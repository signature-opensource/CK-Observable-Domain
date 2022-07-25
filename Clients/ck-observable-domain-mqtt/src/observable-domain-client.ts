import { ObservableDomain, TransactionSetEvent, WatchEvent } from '@signature-code/ck-observable-domain';
import { BehaviorSubject, Observable, Subscription } from 'rxjs';
import { distinctUntilChanged, filter } from 'rxjs/operators';
import { IObservableDomainLeagueDriver } from './iod-league-driver';

(window as any).enableObservableDomainClientLog = false;

export class ObservableDomainClient {
    private readonly evtSub: Subscription;
    private readonly connectionSub: Subscription;
    private readonly stateRoot: BehaviorSubject<ReadonlyArray<any>> = new BehaviorSubject<ReadonlyArray<any>>([]);
    private readonly observableDomainSubject: BehaviorSubject<ObservableDomain | undefined>
        = new BehaviorSubject<ObservableDomain | undefined>(undefined);
    private startWatchingSub?: Subscription;
    private waitingForResync = false;

    private _initEventQueue: TransactionSetEvent[] = [];

    /**
     * The roots of the ObservableDomain loaded by this client.
     * Be careful: This observable IS EMITTED when the ObservableDomain is reloaded.
     * Use pipe(distinctUntilChanged()) to limit downstream impacts.
     */
    public readonly roots$: Observable<ReadonlyArray<any>> = this.stateRoot.pipe(
        filter((x) => x !== undefined && x.length > 0)
    );

    /**
     * The ObservableDomain loaded by this client.
     * Be careful: This observable IS EMITTED on every event.
     * Use pipe(distinctUntilChanged()) to limit downstream impacts.
     */
    public readonly observableDomain$: Observable<ObservableDomain> = this.observableDomainSubject.pipe(
        filter((x): x is ObservableDomain => x !== undefined && x !== null)
    );
    private _roots: ReadonlyArray<any> = [];

    constructor(
        private readonly driverBuilder: () => IObservableDomainLeagueDriver
    ) {
        
        this.evtSub = this.appDomainService
            .getEventsForDomain$(domainName)
            .subscribe((e) => this.onEvent(e));
        this.connectionSub = this.appDomainService.connected$
            .pipe(
                distinctUntilChanged(),
                filter((connected) => connected)
            )
            .subscribe((_) => {
                let lastTransactionNumber = 0;
                // if (this.observableDomainSubject.value !== undefined) {
                //     console.log('Reconnected to ObservableDomain endpoint. Re-watching from last TransactionNumber.');
                //     lastTransactionNumber = this.observableDomainSubject.value.transactionNumber;
                // }
                if (this.startWatchingSub) {
                    this.startWatchingSub.unsubscribe();
                    this.startWatchingSub = undefined;
                }
                this.resyncFrom(lastTransactionNumber);
            });
    }

    get roots(): ReadonlyArray<any> {
        return this._roots;
    }

    public dispose() {
        this.connectionSub.unsubscribe();
        this.evtSub.unsubscribe();
    }

    private applyAndResetEventQueue(od: ObservableDomain) {
        let expectedEvtNum = od.transactionNumber + 1;
        for (let e of this._initEventQueue) {
            if (e.N === expectedEvtNum) {
                console.log(`Domain ${this.domainName}: Applying queued event ${e.N} after init.`);
                od.applyWatchEvent(e);
                expectedEvtNum = od.transactionNumber + 1;
            } else {
                console.warn(`Domain ${this.domainName}: Not applying queued event ${e.N}: Expected transaction number ${expectedEvtNum}.`);
            }
        }
        this._initEventQueue = [];
    }

    private onEvent(evt: WatchEvent) {
        const canLog: boolean = (window as any).enableObservableDomainClientLog;
        const od = this.observableDomainSubject.value;
        if (od === undefined) {
            if (ObservableDomain.isDomainExportEvent(evt)) {
                const od = new ObservableDomain();
                od.applyWatchEvent(evt);
                console.log(
                    `Domain ${this.domainName}: Loaded state. ${od.allObjectsCount} objects in domain. `
                    + `${od.roots.length} roots. Last transaction number: ${od.transactionNumber}`
                );

                this.observableDomainSubject.next(od);

                this.applyAndResetEventQueue(od);
                this.onDomainUpdate();
            } else if (ObservableDomain.isTransactionSetEvent(evt)) {
                console.warn(`Stacking received event ${evt.N}: Domain export was not received yet.`);
                this._initEventQueue.push(evt);
            } else if (ObservableDomain.isErrorEvent(evt)) {
                console.error(
                    `Domain ${this.domainName} error: ${evt.Error}.`
                );
                console.warn('Reloading domain.');
                this.resyncFrom(0);
            }
        } else {
            const currentEventNum = od.transactionNumber;
            const expectedEvtNum = currentEventNum + 1;

            let applyEvent = false;

            if (ObservableDomain.isDomainExportEvent(evt)) {
                // Complete domain export event - apply event as-is
                console.log(
                    `Domain ${this.domainName}: Reloading state from DomainExportEvent.`
                );
                applyEvent = true;
            } else if (ObservableDomain.isTransactionSetEvent(evt)) {
                const firstTransactionNumber = evt.N - evt.E.length + 1;
                // Apply change event
                if (firstTransactionNumber === expectedEvtNum) {
                    // evt.N === expectedEvtNum - Apply events
                    applyEvent = true;
                    if (canLog) console.log(`Applying TransactionNumber ${firstTransactionNumber} to ${evt.N}.`)
                } else if (firstTransactionNumber == currentEventNum) {
                    console.log(`Domain ${this.domainName}: No change from TransactionNumber ${currentEventNum}.`)
                } else if (firstTransactionNumber < expectedEvtNum) {
                    console.warn(
                        `Domain ${this.domainName}: Ignoring received ObservableDomainEvent: `
                        + `event was already applied (Expected TransactionNumber ${expectedEvtNum}, but got ${evt.N})`
                    );
                } else if (firstTransactionNumber > expectedEvtNum) {
                    if (this.waitingForResync) {
                        console.warn(
                            `Domain ${this.domainName}: Received TransactionNumber ${firstTransactionNumber} to ${evt.N} while waiting for ${expectedEvtNum}. Enqueuing.`
                        );
                        this._initEventQueue.push(evt);
                    } else {
                        console.error(
                            `Domain ${this.domainName}: Error while receiving ObservableDomainEvent: some events are missing! `
                            + `Reloading domain. (Expected TransactionNumber ${expectedEvtNum}, but got ${firstTransactionNumber} to ${evt.N})`
                        );

                        this.resyncFrom(currentEventNum);
                    }
                }
            } else if (ObservableDomain.isErrorEvent(evt)) {
                // Domain error - reload completely
                console.error(
                    `Domain ${this.domainName} error: ${evt.Error}.`
                );
                this.resyncFrom(0);
            }

            if (applyEvent) {
                // Apply event as expected
                if (canLog) {
                    console.log(`Domain ${this.domainName}: Applying event`, evt);
                }
                this.waitingForResync = false;
                const od = this.observableDomainSubject.value;
                if (od) {
                    // Run in Zone.js - This makes Angular aware of property changes
                    od.applyWatchEvent(evt);
                    this.applyAndResetEventQueue(od);
                    this.onDomainUpdate();
                }
            }
        }
    }

    public async resyncFrom(transactionNumber: number): Promise<void> {
        if (this.startWatchingSub) {
            this.startWatchingSub.unsubscribe();
            this.startWatchingSub = undefined;
        }
        if (transactionNumber === 0) {
            console.log(`Domain ${this.domainName}: Resetting local ObservableDomain`);
            this._initEventQueue = [];
            this.observableDomainSubject.next(undefined);
        }
        console.log(`Domain ${this.domainName}: Starting watch from transactionNumber ${transactionNumber}...`);
        this.waitingForResync = true;
        this.startWatchingSub = await this.appDomainService
            .startWatching$(this.domainName, transactionNumber)
            .subscribe(_ => {
                this.startWatchingSub.unsubscribe();
                this.startWatchingSub = undefined;
            });
    }

    private onDomainUpdate() {
        const od = this.observableDomainSubject.value;
        if (od) {
            this.stateRoot.next(od.roots);
            this._roots = od.roots;
            if ((window as any).enableObservableDomainClientLog) {
                console.log(`Domain ${this.domainName} update`, this.observableDomainSubject.value);
            }
        }
    }
}
