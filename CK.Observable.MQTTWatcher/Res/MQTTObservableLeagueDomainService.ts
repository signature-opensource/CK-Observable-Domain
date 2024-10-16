import mqtt from "mqtt";
import { WatchEvent } from '../../ObservableDomain/ObservableDomain';
import { IObservableDomainLeagueDriver } from "../../ObservableDomain/IObservableDomainLeagueDriver";
import { MQTTObservableWatcherStartOrRestartCommand } from "./MQTTObservableWatcherStartOrRestartCommand";
import { CrisEndpoint } from "../../Cris/CrisEndpoint";

export class MQTTObservableLeagueDomainService implements IObservableDomainLeagueDriver {

    private client!: mqtt.MqttClient;
    private clientId: string;
    private messageHandlers: ((domainName: string, eventsJson: WatchEvent) => void)[] = [];
    private closeHandlers: ((e: Error | undefined) => void)[] = [(e) => console.log("mqtt disconnected: " + e)];
    constructor(private readonly serverUrl: string, private readonly crisEndpoint: CrisEndpoint, private readonly mqttTopic: string) {
        this.clientId = "od-watcher-" + crypto.randomUUID();
    }

    public async startListeningAsync(domainsNames: { domainName: string; transactionCount: number; }[]): Promise<{ [domainName: string]: WatchEvent; }> {
        const res: { [domainName: string]: WatchEvent; } = {};
        const promises = domainsNames.map(async element => {
            const poco = new MQTTObservableWatcherStartOrRestartCommand(this.clientId, element.domainName, element.transactionCount);
            const result = await this.crisEndpoint.sendOrThrowAsync(poco);
            if (result) {
                res[element.domainName] = JSON.parse(result);
            } else {
                console.warn(`Domain ${element.domainName} doesn't exists.`);
                res[element.domainName] = "";
            }
        });
        await Promise.all(promises);
        return res;
    }

    public async startAsync(): Promise<boolean> {
        try {
            console.log(`connecting to mqtt with client id ${this.clientId}...`)
            this.client = await mqtt.connectAsync(this.serverUrl, {
                clean: true,
                clientId: this.clientId
            });
            this.client.on("message", (a: any, b: any, c: any) => this.handleMessages(a, b, c));
            this.client.on("disconnect", (a: any) => this.handleClose(undefined));
            console.log("connected");
            return true;
        } catch (e) {
            console.log("not connected");
            console.error(e);
            return false;
        }
    }

    private handleMessages(topic: string, payload: Buffer, packet: mqtt.IPublishPacket) {
        console.log("Received MQTT message.");
        if (!topic.startsWith(this.mqttTopic)) return;
        const domainNameSize = payload.readInt32LE();
        const domainName = payload.toString("utf-8", 4, domainNameSize + 4);
        const jsonExportSize = payload.readInt32LE(4 + domainNameSize);
        const jsonExport = payload.toString("utf-8", 4 + 4 + domainNameSize, 4 + 4 + domainNameSize + jsonExportSize);
        this.messageHandlers.forEach(handler => {
            handler(domainName, JSON.parse(jsonExport));
        });
    }

    private handleClose(e: Error | undefined) {
        this.closeHandlers.forEach(handler => {
            handler(e);
        });
    }

    public onMessage(eventHandler: (domainName: string, eventsJson: WatchEvent) => void): void {
        this.messageHandlers.push(eventHandler);

    }
    public onClose(eventHandler: (error: Error | undefined) => void): void {
        this.closeHandlers.push(eventHandler);
    }
    public stopAsync(): Promise<void> {
        return this.client.endAsync(true);
    }
}
