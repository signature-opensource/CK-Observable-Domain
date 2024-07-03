import { ObservableDomain, WatchEvent } from './ObservableDomain';
import { BehaviorSubject, Observable } from 'rxjs';
import { IObservableDomainLeagueDriver } from './IObservableDomainLeagueDriver';

export enum ObservableDomainClientConnectionState {
    Disconnected,
    Connected,
    CatchingUp
}

export class ObservableDomainClient {
    private readonly connectionState: BehaviorSubject<ObservableDomainClientConnectionState> = new BehaviorSubject<ObservableDomainClientConnectionState>(ObservableDomainClientConnectionState.Disconnected);
    public readonly connectionState$: Observable<ObservableDomainClientConnectionState> = this.connectionState.asObservable();
    private stopping = false;
    private buffering = false;
    private bufferedEvents: { domainName: string, watchEvent: WatchEvent }[] = [];
    private onCloseHandler: ((e: Error | undefined) => void) | undefined;
    private readonly domains: {
        [domainName: string]: { domain: ObservableDomain, obs: BehaviorSubject<ReadonlyArray<any>> };
    } = {};
    constructor(
        private readonly driver: IObservableDomainLeagueDriver
    ) {
        this.driver.onMessage(this.onMessage);
        this.driver.onClose(this.onClose);
    }
    private onClose = (e: Error | undefined) => {
        if (this.onCloseHandler != undefined) {
            this.onCloseHandler(e);
        }
    }

    get domainsRoots(): { [domainName: string]: ReadonlyArray<any> } {
        const newObj: { [domainName: string]: ReadonlyArray<any> } = {};
        Object.keys(this.domains).forEach((domainName) => {
            newObj[domainName] = this.domains[domainName].domain.roots;
        });
        return newObj;
    }

    public start() {
        this.loopReconnectAsync();
    }

    public async listenToDomainAsync(domainName: string): Promise<Observable<ReadonlyArray<any>>> {
        if (this.domains[domainName] === undefined) {
            const od = new ObservableDomain();
            const subject = new BehaviorSubject<ReadonlyArray<any>>(od.roots);
            this.domains[domainName] = {
                domain: od,
                obs: subject
            };
            if (this.connectionState.value != ObservableDomainClientConnectionState.Disconnected) {
                const res = (await this.driver.startListeningAsync([{ domainName: domainName, transactionCount: 0 }]))[domainName];
                od.applyWatchEvent(res);
            }

        }
        return this.domains[domainName].obs;
    }

    private async loopReconnectAsync(): Promise<void> {
        while (!this.stopping) {
            try {
                this.buffering = true;
                this.bufferedEvents = [];
                if (!await this.driver.startAsync()) continue;
                this.connectionState.next(ObservableDomainClientConnectionState.CatchingUp);
                const domainExports = await this.driver.startListeningAsync(Object.keys(this.domains).map((d => {
                    return {
                        domainName: d,
                        transactionCount: this.domains[d]?.domain.transactionNumber ?? 0
                    }
                })));
                this.buffering = false;
                Object.keys(domainExports).forEach((domainName) => {
                    this.onMessage(domainName, domainExports[domainName]);
                    const currDomain = this.domains[domainName];
                    console.log(
                        `Domain ${domainName}: ${currDomain.domain.allObjectsCount} objects. `
                        + `${currDomain.domain.roots.length} root(s). Current Transaction Number: ${currDomain.domain.transactionNumber}`
                    );
                });
                for (const event of this.bufferedEvents) {
                    this.onMessage(event.domainName, event.watchEvent);
                }

                this.connectionState.next(ObservableDomainClientConnectionState.Connected);
                await new Promise<void>(
                    (resolve) => {
                        this.onCloseHandler = ((error) => {
                            if (error != null) {
                                console.log("OD driver Disconnected due to : " + error);
                            } else {
                                console.log("OD driver Disconnected for an unknown reason");
                            }
                            resolve();
                        });
                    }
                );
            }
            catch (e) {
                console.error("Error in OD reconnect loop:", e);
                //we don't throw since we retry forever to reconnect.
            }
            finally {
                this.connectionState.next(ObservableDomainClientConnectionState.Disconnected);
                this.driver.stopAsync();
            }

        }
    }

    public async stopAsync(): Promise<void> {
        this.stopping = true;
        await this.driver.stopAsync();
        this.connectionState.complete();
    }

    private onMessage = (domainName: string, event: WatchEvent) => {
        const curr = this.domains[domainName];
        if (curr === undefined) {
            console.log(`Received event for unknown domain ${domainName}, ignoring this event.`);
            return;
        }
        if (this.buffering) {
            console.log("OD Client is starting, buffering an event for " + domainName);
            this.bufferedEvents.push({ domainName: domainName, watchEvent: event });
            return;
        }

        try {
            if (ObservableDomain.isTransactionSetEvent(event)) {
                if (event.N <= curr.domain.transactionNumber) {
                    console.warn(`Ignoring received past event. Current TN: ${curr.domain.transactionNumber}. New TN: ${event.N}`);
                    return;
                }
                console.log(`Received OD event for domain ${domainName}. Current TN: ${curr.domain.transactionNumber}. New TN: ${event.N}`);
            } else if (ObservableDomain.isDomainExportEvent(event)) {
                console.log(`Received domain export for domain ${domainName}. New TN: ${event.N}`);
            }
            curr.domain.applyWatchEvent(event);
            curr.obs.next(curr.domain.roots);
        } catch (e) {
            console.error(e);
            curr.domain = new ObservableDomain(); // reset the domain
            this.driver.stopAsync(); // kill the whole connection, which will reload this domain from scratch.
            return;
        }
    }
}
