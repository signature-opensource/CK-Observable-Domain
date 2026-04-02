import {
    CrisEndpoint,
    IObservableDomainConnection,
    ObservableDomainWatcherStartOrRestartCommand,
    WatchEvent
} from "@local/ck-gen";

export class WebSocketObservableDomainConnection implements IObservableDomainConnection {
    private readonly messageHandlers: Array<(domainName: string, eventsJson: WatchEvent) => void> = [];
    private readonly closeHandlers: Array<(error: (Error | undefined)) => void> = [];

    private connection?: WebSocket;
    private connectionId?: string;

    constructor(
        private readonly websocketUrl: string,
        private readonly crisEndpoint: CrisEndpoint
    ) { }

    public async startAsync(): Promise<boolean> {
        try {
            const { webSocket, connectionId } = await this.createWebSocketAsync(this.websocketUrl);
            this.connection = webSocket;
            this.connectionId = connectionId;
            return true;
        } catch (e) {
            console.error(e);
            return false;
        }
    }

    private createWebSocketAsync(url: string): Promise<{ webSocket: WebSocket, connectionId: string }> {
        return new Promise<{ webSocket: WebSocket, connectionId: string }>(async (resolve, reject) => {
            const socket = new WebSocket(url);
            socket.onerror = (error) => reject(error);
            socket.onmessage = (event) => this.handleOnMessageEvents(event, { socket, resolve, reject });
            socket.onclose = (event) => this.handleOnCloseEvents(event);
        });
    }

    private handleOnMessageEvents(
        event: MessageEvent,
        negociation: {
            socket: WebSocket,
            resolve: (value: (PromiseLike<{ webSocket: WebSocket; connectionId: string }> | { webSocket: WebSocket; connectionId: string })) => void;
            reject: (reason?: any) => void
        }): void {
        const data: { connectionId: string } | [ string, WatchEvent ] = JSON.parse(event.data);
        if( !WebSocketObservableDomainConnection.isWatchEventMessage(data) ) {
            if( this.connectionId !== undefined )
                throw new Error("Connection already started");

            this.connectionId = data.connectionId;
            negociation.resolve({ webSocket: negociation.socket, connectionId: this.connectionId });
            return;
        } else {
            const [ domainName, watchEvent ] = data;
            for (const messageHandler of this.messageHandlers) {
                messageHandler(domainName, watchEvent);
            }
        }
    }

    private handleOnCloseEvents(
        event: CloseEvent
    ): void {
        this.connection = this.connectionId = undefined;
        const error: Error | undefined = event.wasClean ? undefined : new Error(event.reason);
        for (const closeHandler of this.closeHandlers) {
            closeHandler(error);
        }
    }

    public async startListeningAsync(domains: { domainName: string; transactionCount: number }[]): Promise<{ [p: string]: WatchEvent }> {
        if( this.connectionId === undefined )
            throw new Error("Connection not started");

        const res: { [domainName: string]: WatchEvent } = {};
        const promises = domains.map(async domain => {
            const command = new ObservableDomainWatcherStartOrRestartCommand(this.connectionId, domain.domainName, domain.transactionCount);
            const result = await this.crisEndpoint.sendOrThrowAsync(command);
            if (result) {
                res[domain.domainName] = JSON.parse(result);
            } else {
                console.warn(`Domain ${domain.domainName} doesn't exists.`);
                res[domain.domainName] = "";
            }
        });
        await Promise.all(promises);
        return res;
    }

    public onMessage(eventHandler: (domainName: string, eventsJson: WatchEvent) => void): void {
        this.messageHandlers.push(eventHandler);
    }

    public onClose(eventHandler: (error: (Error | undefined)) => void): void {
        this.closeHandlers.push(eventHandler);
    }

    public stopAsync(): Promise<void> {
        if(this.connection === undefined)
        {
            console.warn("Connection was not started");
            return Promise.resolve();
        }

        return new Promise<void>((resolve, reject) => {
          try {
              this.connection!.addEventListener("close", () => { resolve() }, { once: true })
              this.connection!.close();
          }  catch (e) {
              console.error(e);
              reject();
          }
        })
    }

    private static isWatchEventMessage( message: { connectionId: string } | [ string, WatchEvent ] ): message is [ string, WatchEvent ] {
        return Array.isArray(message);
    }
}
