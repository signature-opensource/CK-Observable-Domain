import { ICommandModel, ICommand } from "../../Cris/Model";
import { CTSType } from "../../Core/CTSType";

export class MQTTObservableWatcherStartOrRestartCommand implements ICommand<string|undefined> {
public mqttClientId: string;
public domainName: string;
public transactionNumber: number;
public constructor()
public constructor(
mqttClientId?: string,
domainName?: string,
transactionNumber?: number)
constructor(
mqttClientId?: string,
domainName?: string,
transactionNumber?: number)
{
this.mqttClientId = mqttClientId ?? "";
this.domainName = domainName ?? "";
this.transactionNumber = transactionNumber ?? 0;
CTSType["CK.Observable.MQTTWatcher.IMQTTObservableWatcherStartOrRestartCommand"].set( this );
}

get commandModel(): ICommandModel { return MQTTObservableWatcherStartOrRestartCommand.#m; }

static #m =  {
applyAmbientValues( command: any, a: any, o: any ) {
// This command has no AmbientValue property.

}

}
readonly _brand!: ICommand<string|undefined>["_brand"] & {"7":any};
}
