import { CommandModel } from "../../Cris/Model";
import { SymbolType } from "../../Core/IPoco";

export class SignalRObservableWatcherStartOrRestartCommand {
get commandModel() { return SignalRObservableWatcherStartOrRestartCommand._commandModel; }
private static readonly _commandModel: CommandModel<string|undefined> =  {
commandName: "CK.Observable.SignalRWatcher.ISignalRObservableWatcherStartOrRestartCommand",
applyAmbientValues( command: any, a: any, o: any ) {
// This command has no property that appear in the Ambient Values.

}

};
clientId: string;
domainName: string;
transactionNumber: number;

[SymbolType] = "CK.Observable.SignalRWatcher.ISignalRObservableWatcherStartOrRestartCommand";
/**
 * This SHOULD NOT be called! It's unfortunately public (waiting for the Default Values issue to be solved).
 **/
constructor() {
this.clientId = undefined!;
this.domainName = undefined!;
this.transactionNumber = undefined!;

}
/**
 * Factory method that exposes all the properties as parameters.
 **/
static create( 
clientId: string,
domainName: string,
transactionNumber: number ) : SignalRObservableWatcherStartOrRestartCommand

/**
 * Creates a new command and calls a configurator for it.
 * @param config A function that configures the new command.
 **/
static create( config: (c: SignalRObservableWatcherStartOrRestartCommand) => void ) : SignalRObservableWatcherStartOrRestartCommand
// Implementation.
static create( 
clientId: string|((c:SignalRObservableWatcherStartOrRestartCommand) => void),
domainName?: string,
transactionNumber?: number ) {
if( typeof clientId === 'function' ) {
const c = new SignalRObservableWatcherStartOrRestartCommand();
clientId(c);
return c;
}
const c = new SignalRObservableWatcherStartOrRestartCommand();
c.clientId = clientId;
c.domainName = domainName!;
c.transactionNumber = transactionNumber!;
return c;
}
}
