import { CommandModel, ICrisEndpoint } from '../../Cris/Model';
import { SymbolType } from '../../Core/IPoco';

export class MQTTObservableWatcherStartOrRestartCommand {
readonly commandModel: CommandModel<string|undefined> =  {
commandName: 'CK.Observable.MQTTWatcher.IMQTTObservableWatcherStartOrRestartCommand',
isFireAndForget: false,
send: (e: ICrisEndpoint) => e.send( this ),
applyAmbientValues: (values: { [index: string]: any }, force?: boolean ) =>  {
// This command has no property that appear in the Ambient Values.

}

};
mqttClientId: string;
domainName: string;
transactionNumber: number;

[SymbolType]: 'CK.Observable.MQTTWatcher.IMQTTObservableWatcherStartOrRestartCommand';
// This SHOULD NOT be called! It's unfortunately public (waiting for the Default Values issue to be solved).
/*private*/ constructor() {
this.mqttClientId = undefined!;
this.domainName = undefined!;
this.transactionNumber = undefined!;

}
/**
 * Factory method that exposes all the properties as parameters.
 **/
static create( 
mqttClientId: string,
domainName: string,
transactionNumber: number ) : MQTTObservableWatcherStartOrRestartCommand

/**
 * Creates a new command and calls a configurator for it.
 * @param config A function that configures the new command.
 **/
static create( config: (c: MQTTObservableWatcherStartOrRestartCommand) => void ) : MQTTObservableWatcherStartOrRestartCommand
// Implementation.
static create( 
mqttClientId: string|((c:MQTTObservableWatcherStartOrRestartCommand) => void),
domainName?: string,
transactionNumber?: number ) {
const c = new MQTTObservableWatcherStartOrRestartCommand();
if( typeof mqttClientId === 'function' ) mqttClientId(c);
else {
c.mqttClientId = mqttClientId;
c.domainName = domainName!;
c.transactionNumber = transactionNumber!;
}
return c;
}
}
