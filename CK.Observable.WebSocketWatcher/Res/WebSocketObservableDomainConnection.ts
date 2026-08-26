import {
    CrisEndpoint,
    IObservableDomainConnection,
    ObservableDomainWatcherStartOrRestartCommand,
    WatchEvent,
    WSConnection
} from "@local/ck-gen";

/**
 * Channel topic of the observable domain events. Must stay in sync with
 * `ObservableDomainWatcherManager.Topic`.
 */
const OBSERVABLE_TOPIC = 'CK.Observable.WebSocketWatcher';

// Backoff of the negotiation, doubling and capped, mirroring the one WSConnection applies to the socket
// itself. It covers the other half of the problem: ObservableDomainClient retries in a loop that has no
// delay of its own, and now that the socket is shared and persistent, startAsync() no longer costs a
// handshake - so a negotiation that keeps failing (an expired token, typically) would spin at full
// speed. The socket being up is not enough to conclude that retrying is free.
const RETRY_MIN_MS = 1000;
const RETRY_MAX_MS = 30000;

/**
 * Adapts the one WSConnection of the application to IObservableDomainConnection.
 *
 * This owns no socket: it claims the `OD` topic on the shared connection, and closing that connection
 * would take every other feature down with it.
 *
 * Which makes this class the only thing that can tell ObservableDomainClient that its watch is over.
 * The client has two ways of ending one - the socket dropped, or a domain event failed to apply - and
 * both must end in onClose, because that is what its reconnect loop is parked on. So the two converge
 * on close(): whichever happens first releases the topic and notifies, the other becomes a no-op.
 *
 * Reconnection itself belongs to WSConnection; startAsync() just waits for the next one. Note that the
 * domain-error path no longer needs a socket to die to recover: the loop comes straight back, and the
 * next startListeningAsync() re-exports the domain it had reset.
 */
export class WebSocketObservableDomainConnection implements IObservableDomainConnection {
    private readonly messageHandlers: Array<(domainName: string, eventsJson: WatchEvent) => void> = [];
    private readonly closeHandlers: Array<(error: (Error | undefined)) => void> = [];
    // Whether the `OD` topic is currently ours. Guards close() so that the two ways of ending a watch
    // cannot notify twice.
    private started = false;
    // Consecutive negotiations that failed. Reset by a successful one.
    private failedNegotiations = 0;

    constructor(
        private readonly wsConnection: WSConnection,
        private readonly crisEndpoint: CrisEndpoint
    ) { }

    public async startAsync(): Promise<boolean> {
        try {
            await this.throttleAsync();
            await this.wsConnection.whenConnectedAsync();
            // Overwrites any previous registration: this is called again on every turn of the client
            // reconnect loop. onClosed rides with it, so a client that has stopped watching is not
            // notified of a loss it no longer cares about.
            this.wsConnection.addHandler(OBSERVABLE_TOPIC, {
                onMessage: message => this.handleMessage(message),
                onClosed: error => this.close(error)
            });
            this.started = true;
            return true;
        } catch (e) {
            console.error(e);
            return false;
        }
    }

    public async startListeningAsync(domains: { domainName: string; transactionCount: number }[]): Promise<{ [p: string]: WatchEvent }> {
        const connectionId = this.wsConnection.connectionId;
        // !started covers the case where the connection dropped since startAsync(): the identifier may
        // already be that of a fresh socket, but our topic is no longer registered, so listening on it
        // would be pointless.
        if( connectionId === undefined || !this.started ) {
            this.failedNegotiations++;
            throw new Error("Connection not started");
        }

        try {
            const res: { [domainName: string]: WatchEvent } = {};
            const promises = domains.map(async domain => {
                const command = new ObservableDomainWatcherStartOrRestartCommand(connectionId, domain.domainName, domain.transactionCount);
                const result = await this.crisEndpoint.sendOrThrowAsync(command);
                if (result) {
                    res[domain.domainName] = JSON.parse(result);
                } else {
                    console.warn(`Domain ${domain.domainName} doesn't exists.`);
                    res[domain.domainName] = "";
                }
            });
            await Promise.all(promises);
            // The connection can drop while the commands are in flight, and reporting the close is not
            // enough here: ObservableDomainClient installs its onCloseHandler only after this method
            // returns, so a close raised now is swallowed - and on later turns of its loop it is
            // swallowed by the stale handler of the previous turn, whose promise is already resolved. It
            // would then park on a close that can no longer come, since our topic is unregistered.
            // Throwing sends it to its catch instead, and the loop starts over.
            if (!this.started) throw new Error("Connection lost while starting to listen.");
            // Only a completed negotiation proves the round trip healthy: resetting on a mere connection
            // would let a server that accepts sockets but refuses commands be hammered.
            this.failedNegotiations = 0;
            return res;
        } catch (e) {
            this.failedNegotiations++;
            throw e;
        }
    }

    public onMessage(eventHandler: (domainName: string, eventsJson: WatchEvent) => void): void {
        this.messageHandlers.push(eventHandler);
    }

    public onClose(eventHandler: (error: (Error | undefined)) => void): void {
        this.closeHandlers.push(eventHandler);
    }

    /**
     * Stops watching: releases the `OD` topic and reports the close. The socket is shared, so it is not
     * ours to close - only the application closes it, through WSConnection.stopAsync().
     *
     * Reporting is the point. ObservableDomainClient calls this to end a watch it wants to restart (a
     * domain event that failed to apply) and its reconnect loop is waiting on onClose to do so: it used
     * to get that from the dying socket, and now it gets it from here.
     */
    public stopAsync(): Promise<void> {
        this.close(undefined);
        return Promise.resolve();
    }

    // Waits before retrying a negotiation, and returns at once on the nominal path where nothing has
    // failed. This is what turns ObservableDomainClient's delay-free retry loop into a backoff: it comes
    // straight back here after any failure, and there is no handshake left to slow it down.
    private throttleAsync(): Promise<void> {
        if (this.failedNegotiations === 0) return Promise.resolve();
        const delay = Math.min(RETRY_MIN_MS * Math.pow(2, this.failedNegotiations - 1), RETRY_MAX_MS);
        console.warn(`Observable domain negotiation failed ${this.failedNegotiations} time(s), retrying in ${delay} ms.`);
        return new Promise<void>(resolve => setTimeout(resolve, delay));
    }

    // The payload of the OD topic is the very frame the server has always sent: [domainName, watchEvent].
    private handleMessage(message: unknown): void {
        const [ domainName, watchEvent ] = message as [ string, WatchEvent ];
        for (const messageHandler of this.messageHandlers) {
            messageHandler(domainName, watchEvent);
        }
    }

    // Idempotent, and that is what keeps the two causes from notifying twice: on a socket loss onClosed
    // gets here first, then the loop calls stopAsync() in its finally and finds nothing left to do.
    private close(error: Error | undefined): void {
        if (!this.started) return;
        this.started = false;
        this.wsConnection.removeHandler(OBSERVABLE_TOPIC);
        for (const closeHandler of this.closeHandlers) {
            closeHandler(error);
        }
    }
}
