import { IObservableDomainConnection, ObservableDomainClient, ObservableDomainClientConnectionState, WatchEvent } from '@local/ck-gen';
import { firstValueFrom } from 'rxjs';
import { filter } from 'rxjs/operators';

// The object 0 is an empty ObservableList (a typed array "A"). The "I" event inserts an item in it.
const exportAt3 = '{"N":3,"P":[],"O":[[{"þ":[0,"A"]}]],"R":[],"S":[],"C":1}';

/**
 * A connection where the test decides when the answer of startListeningAsync arrives,
 * and pushes events in the meantime.
 */
class TestConnection implements IObservableDomainConnection {
    private messageHandler: ((domainName: string, e: WatchEvent) => void) | undefined;
    private pendingAnswer: ((answer: { [domainName: string]: WatchEvent }) => void) | undefined;
    private pendingFailure: ((error: Error) => void) | undefined;
    stopCount = 0;

    async startAsync(): Promise<boolean> { return true; }

    startListeningAsync(domains: { domainName: string, transactionCount: number }[]): Promise<{ [domainName: string]: WatchEvent }> {
        // The reconnect loop starts with no domain.
        if (domains.length === 0) return Promise.resolve({});
        return new Promise((resolve, reject) => {
            this.pendingAnswer = resolve;
            this.pendingFailure = reject;
        });
    }

    onMessage(handler: (domainName: string, e: WatchEvent) => void): void { this.messageHandler = handler; }

    onClose(): void { }

    async stopAsync(): Promise<void> { this.stopCount++; }

    push(domainName: string, json: string): void { this.messageHandler!(domainName, JSON.parse(json)); }

    answer(domainName: string, json: string): void { this.pendingAnswer!({ [domainName]: JSON.parse(json) }); }

    fail(): void { this.pendingFailure!(new Error("The connection is lost.")); }

    get hasPendingAnswer(): boolean { return this.pendingAnswer !== undefined; }
}

async function connectedClient(connection: TestConnection): Promise<ObservableDomainClient> {
    const client = new ObservableDomainClient(connection);
    client.start();
    await firstValueFrom(client.connectionState$.pipe(filter(s => s === ObservableDomainClientConnectionState.Connected)));
    return client;
}

async function waitForPendingAnswer(connection: TestConnection): Promise<void> {
    while (!connection.hasPendingAnswer) await new Promise(r => setTimeout(r, 1));
}

describe('ObservableDomainClient.listenToDomainAsync - ', () => {

    it('a push that arrives before the export is applied after it', async () => {
        const connection = new TestConnection();
        const client = await connectedClient(connection);

        const listening = client.listenToDomainAsync('D');
        await waitForPendingAnswer(connection);
        // The server subscribes to the pushes before it computes the export: n°4 can arrive first.
        connection.push('D', '{"N":4,"E":[[["I",0,0,"One"]]],"L":3}');
        connection.answer('D', exportAt3);
        await listening;

        const d = client.getDomain('D')!;
        expect(d.transactionNumber).toBe(4);
        expect(Array.from(d.allObjects)[0]).toEqual(["One"]);
        expect(connection.stopCount).toBe(0);
    });

    it('a push that is already in the export is ignored', async () => {
        const connection = new TestConnection();
        const client = await connectedClient(connection);

        const listening = client.listenToDomainAsync('D');
        await waitForPendingAnswer(connection);
        connection.push('D', '{"N":3,"E":[[["I",0,0,"Already in the export."]]],"L":2}');
        connection.answer('D', exportAt3);
        await listening;

        const d = client.getDomain('D')!;
        expect(d.transactionNumber).toBe(3);
        expect(Array.from(d.allObjects)[0]).toEqual([]);
        expect(connection.stopCount).toBe(0);
    });

    it('the pushes are dropped when the export request fails', async () => {
        const connection = new TestConnection();
        const client = await connectedClient(connection);

        const listening = client.listenToDomainAsync('D');
        await waitForPendingAnswer(connection);
        connection.push('D', exportAt3);
        connection.fail();
        await expect(listening).rejects.toThrow("The connection is lost.");

        // The reconnect loop asks again for the export: an old buffered push must not be applied after it.
        const d = client.getDomain('D')!;
        expect(d.transactionNumber).toBe(0);
    });
});
