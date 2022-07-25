import { AsyncMqttClient, IPublishPacket, connectAsync } from "async-mqtt";
import { Observable, Subject } from 'rxjs';
import { filter, map, share } from 'rxjs/operators';
import { WatchEvent } from '@signature-code/ck-observable-domain';
import { IObservableDomainLeagueDriver } from "./iod-league-driver";
import { json } from "stream/consumers";

export class MqttObservableLeagueDomainService implements IObservableDomainLeagueDriver {

    private client: AsyncMqttClient;
    constructor(private readonly serverUrl: string, private readonly mqttTopic: string) {
    }

    public async start(): Promise<void> {
        this.client = await connectAsync(this.serverUrl, {
            clean: true
        }, true);
    }

    public onMessage(eventHandler: (domainName: string, eventsJson: WatchEvent) => void): void {
        this.client.on("message", (topic: string, payload: Buffer, packet: IPublishPacket) => {
            if (!topic.startsWith(this.mqttTopic)) return;
            const domainNameSize = payload.readInt32LE();
            const domainName = payload.toString("utf-8", 4, domainNameSize);
            const jsonExportSize = payload.readInt32LE(4 + domainNameSize);
            const jsonExport = payload.toString("utf-8", 4 + 4 + domainNameSize, jsonExportSize);
            eventHandler(domainName, JSON.parse(jsonExport));
        })
    }
    public onClose(eventHandler: (error: Error) => void): void {
        this.client.on("disconnect", ()=> {
            eventHandler(null);
        })
    }
    public async stop(): Promise<void> {
        await this.client.end(true);
    }
}

