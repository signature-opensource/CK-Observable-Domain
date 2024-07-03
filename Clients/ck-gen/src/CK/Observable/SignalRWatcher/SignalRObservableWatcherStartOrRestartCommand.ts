import { ICommandModel, ICommand } from "../../Cris/Model";
import { CTSType } from "../../Core/CTSType";

export class SignalRObservableWatcherStartOrRestartCommand implements ICommand<string|undefined> {
public clientId: string;
public domainName: string;
public transactionNumber: number;
public constructor()
public constructor(
clientId?: string,
domainName?: string,
transactionNumber?: number)
constructor(
clientId?: string,
domainName?: string,
transactionNumber?: number)
{
this.clientId = clientId ?? "";
this.domainName = domainName ?? "";
this.transactionNumber = transactionNumber ?? 0;
CTSType["CK.Observable.SignalRWatcher.ISignalRObservableWatcherStartOrRestartCommand"].set( this );
}

get commandModel(): ICommandModel { return SignalRObservableWatcherStartOrRestartCommand.#m; }

static #m =  {
applyAmbientValues( command: any, a: any, o: any ) {
// This command has no AmbientValue property.

}

}
readonly _brand!: ICommand<string|undefined>["_brand"] & {"10":any};
}
