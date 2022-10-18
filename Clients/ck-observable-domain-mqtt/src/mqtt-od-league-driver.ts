import { AsyncMqttClient, IPublishPacket, connectAsync, AsyncClient } from "async-mqtt";
import { Observable, Subject } from 'rxjs';
import { filter, map, share } from 'rxjs/operators';
import { WatchEvent } from '@signature-code/ck-observable-domain';
import { IObservableDomainLeagueDriver } from "./iod-league-driver";
import { json } from "stream/consumers";
import { ICrisEndpoint, MQTTObservableWatcherStartOrRestartCommand, VESACode } from "@signature/generated";
import { debug } from "console";

export class MqttObservableLeagueDomainService implements IObservableDomainLeagueDriver {

    private client: AsyncMqttClient;
    private clientId: string;
    constructor(private readonly serverUrl: string, private readonly crisEndpoint: ICrisEndpoint, private readonly mqttTopic: string) {
        this.clientId = "od-watcher-"+ crypto.randomUUID();
    }

    async startListening(domainsNames: { domainName: string; transactionCount: number; }[]): Promise<{ [domainName: string]: WatchEvent; }> {
        const res = {};
        const promises = domainsNames.map( async element => {
            const poco = MQTTObservableWatcherStartOrRestartCommand.create(this.clientId, element.domainName, element.transactionCount);
            const resp = await this.crisEndpoint.send(poco);
            if(resp.code == 'CommunicationError' ) throw resp.result;
            if(resp.code != VESACode.Synchronous) throw new Error(JSON.stringify(resp.result));
            res[element.domainName] = JSON.parse(resp.result);
        });
        await Promise.all(promises);
        return res;
    }

    public async start(): Promise<boolean> {
        try {
            console.log(`connecting to mqtt with client id ${this.clientId}...`)
            this.client = await connectAsync(this.serverUrl, {
                clean: true,
                clientId: this.clientId
            }, true);
            console.log("connected");
            return true;
        } catch (e) {
            console.log("not connected");
            console.error(e);
            return false;
        }
    }

    public onMessage(eventHandler: (domainName: string, eventsJson: WatchEvent) => void): void {
        this.client.on("message", (topic: string, payload: Buffer, packet: IPublishPacket) => {
            if (!topic.startsWith(this.mqttTopic)) return;
            const domainNameSize = payload.readInt32LE();
            const domainName = payload.toString("utf-8", 4, domainNameSize+4);
            const jsonExportSize = payload.readInt32LE(4 + domainNameSize);
            const jsonExport = payload.toString("utf-8", 4 + 4 + domainNameSize, 4 + 4 + domainNameSize+jsonExportSize);
            eventHandler(domainName, JSON.parse(jsonExport));
        });
    }
    public onClose(eventHandler: (error: Error) => void): void {
        if (!this.client.connected) {
            eventHandler(null);
            return;
        }
        this.client.on("disconnect", () => {
            console.log("mqtt disconnected");
            eventHandler(null);
        })
    }
    public async stop(): Promise<void> {
        await this.client.end(true);
    }
}

