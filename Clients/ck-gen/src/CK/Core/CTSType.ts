import { SliderCommand } from "../Observable/ServerSample/App/SliderCommand";
import { MQTTObservableWatcherStartOrRestartCommand } from "../Observable/MQTTWatcher/MQTTObservableWatcherStartOrRestartCommand";
import { SignalRObservableWatcherStartOrRestartCommand } from "../Observable/SignalRWatcher/SignalRObservableWatcherStartOrRestartCommand";
import { AspNetResult } from "../Cris/AspNet/AspNetResult";
import { AspNetCrisResultError } from "../Cris/AspNet/AspNetCrisResultError";
import { AmbientValues } from "../Cris/AmbientValues/AmbientValues";
import { AmbientValuesCollectCommand } from "../Cris/AmbientValues/AmbientValuesCollectCommand";
import { SimpleUserMessage } from "./SimpleUserMessage";

export const CTSType: any  = {
    toTypedJson( o: any ) : unknown {
        if( o == null ) return null;
        const t = o[SymCTS];
        if( !t ) throw new Error( "Untyped object. A type must be specified with CTSType." );
        return [t.name, t.json( o )];
    },
    fromTypedJson( o: any ) : unknown {
        if( o == null ) return undefined;
        if( !(o instanceof Array && o.length === 2) ) throw new Error( "Expected 2-cells array." );
        var t = CTSType[o[0]];
        if( !t ) throw new Error( `Invalid type name: ${o[0]}.` );
        if( !t.set ) throw new Error( `Type name '${o[0]}' is not serializable.` );
        const j = t.nosj( o[1] );
        return j !== null && typeof j === 'object' ? t.set( j ) : j;
   },
   stringify( o: any, withType: boolean = true ) : string {
       var t = CTSType.toTypedJson( o );
       return JSON.stringify( withType ? t : t[1] );
   },
   parse( s: string ) : unknown {
       return CTSType.fromTypedJson( JSON.parse( s ) );
   },
"CK.Observable.ServerSample.App.ISliderCommand": {
name: "CK.Observable.ServerSample.App.ISliderCommand",
set( o: SliderCommand ): SliderCommand {  (o as any)[SymCTS] = this; return o; },
json( o: any ) {return o;
},
nosj( o: any ) {if( o == null ) return undefined;
return new SliderCommand(
CTSType["float"].nosj( o.sliderValue ) );
},
},
"CK.Observable.MQTTWatcher.IMQTTObservableWatcherStartOrRestartCommand": {
name: "CK.Observable.MQTTWatcher.IMQTTObservableWatcherStartOrRestartCommand",
set( o: MQTTObservableWatcherStartOrRestartCommand ): MQTTObservableWatcherStartOrRestartCommand {  (o as any)[SymCTS] = this; return o; },
json( o: any ) {return o;
},
nosj( o: any ) {if( o == null ) return undefined;
return new MQTTObservableWatcherStartOrRestartCommand(
CTSType["string"].nosj( o.mqttClientId ),
CTSType["string"].nosj( o.domainName ),
CTSType["int"].nosj( o.transactionNumber ) );
},
},
"CK.Observable.SignalRWatcher.ISignalRObservableWatcherStartOrRestartCommand": {
name: "CK.Observable.SignalRWatcher.ISignalRObservableWatcherStartOrRestartCommand",
set( o: SignalRObservableWatcherStartOrRestartCommand ): SignalRObservableWatcherStartOrRestartCommand {  (o as any)[SymCTS] = this; return o; },
json( o: any ) {return o;
},
nosj( o: any ) {if( o == null ) return undefined;
return new SignalRObservableWatcherStartOrRestartCommand(
CTSType["string"].nosj( o.clientId ),
CTSType["string"].nosj( o.domainName ),
CTSType["int"].nosj( o.transactionNumber ) );
},
},
"AspNetResult": {
name: "AspNetResult",
set( o: AspNetResult ): AspNetResult {  (o as any)[SymCTS] = this; return o; },
json( o: any ) {if( !o ) return null;
let r = {} as any;
r.result = CTSType.toTypedJson( o.result );
r.validationMessages = o.validationMessages;
r.correlationId = o.correlationId;
return r;
},
nosj( o: any ) {if( o == null ) return undefined;
return new AspNetResult(
CTSType.fromTypedJson( o.result ),
CTSType["L(SimpleUserMessage)"].nosj( o.validationMessages ),
CTSType["string"].nosj( o.correlationId ) );
},
},
"AspNetCrisResultError": {
name: "AspNetCrisResultError",
set( o: AspNetCrisResultError ): AspNetCrisResultError {  (o as any)[SymCTS] = this; return o; },
json( o: any ) {return o;
},
nosj( o: any ) {if( o == null ) return undefined;
return new AspNetCrisResultError(
CTSType["bool"].nosj( o.isValidationError ),
CTSType["L(string)"].nosj( o.errors ),
CTSType["string"].nosj( o.logKey ) );
},
},
"CK.Cris.AmbientValues.IAmbientValues": {
name: "CK.Cris.AmbientValues.IAmbientValues",
set( o: AmbientValues ): AmbientValues {  (o as any)[SymCTS] = this; return o; },
json( o: any ) {return o;
},
nosj( o: any ) {if( o == null ) return undefined;
return new AmbientValues(
CTSType["string"].nosj( o.currentCultureName ),
CTSType["int"].nosj( o.actorId ),
CTSType["int"].nosj( o.actualActorId ),
CTSType["string"].nosj( o.deviceId ) );
},
},
"CK.Cris.AmbientValues.IAmbientValuesCollectCommand": {
name: "CK.Cris.AmbientValues.IAmbientValuesCollectCommand",
set( o: AmbientValuesCollectCommand ): AmbientValuesCollectCommand {  (o as any)[SymCTS] = this; return o; },
json( o: any ) {return o;
},
nosj( o: any ) {if( o == null ) return undefined;
return new AmbientValuesCollectCommand(
 );
},
},
"CK.Cris.ICommand<CK.Cris.AmbientValues.IAmbientValues>": {
name: "CK.Cris.ICommand<CK.Cris.AmbientValues.IAmbientValues>",
},
"string": {
name: "string",
set( o: string ): string { o = Object( o ); (o as any)[SymCTS] = this; return o; },
json( o: any ) {return o; },
nosj( o: any ) {return o == null ? undefined : o; },
},
"CK.Cris.ICommand<string>": {
name: "CK.Cris.ICommand<string>",
},
"CK.Cris.ICommand": {
name: "CK.Cris.ICommand",
},
"CK.Cris.IAbstractCommand": {
name: "CK.Cris.IAbstractCommand",
},
"CK.Cris.ICrisPoco": {
name: "CK.Cris.ICrisPoco",
},
"CK.Core.IPoco": {
name: "CK.Core.IPoco",
},
"int": {
name: "int",
set( o: number ): number { o = Object( o ); (o as any)[SymCTS] = this; return o; },
json( o: any ) {return o; },
nosj( o: any ) {return o == null ? undefined : o; },
},
"bool": {
name: "bool",
set( o: boolean ): boolean { o = Object( o ); (o as any)[SymCTS] = this; return o; },
json( o: any ) {return o; },
nosj( o: any ) {return o == null ? undefined : o; },
},
"object": {
name: "object",
},
"SimpleUserMessage": {
name: "SimpleUserMessage",
set( o: SimpleUserMessage ): SimpleUserMessage {  (o as any)[SymCTS] = this; return o; },
json( o: any ) {return o; },
nosj( o: any ) {return o != null ? SimpleUserMessage.parse( o ) : undefined; },
},
"L(SimpleUserMessage)": {
name: "L(SimpleUserMessage)",
set( o: Array<SimpleUserMessage> ): Array<SimpleUserMessage> {  (o as any)[SymCTS] = this; return o; },
json( o: any ) {
return o;
},
nosj( o: any ) {
if( o == null ) return undefined;
if( !(o instanceof Array) ) throw new Error( 'Expected Array.' );
const t = CTSType["SimpleUserMessage"];
return o.map( t.nosj );
},
},
"L(string?)": {
name: "L(string?)",
set( o: Array<string|undefined> ): Array<string|undefined> {  (o as any)[SymCTS] = this; return o; },
json( o: any ) {
return o;
},
nosj( o: any ) {
if( o == null ) return undefined;
if( !(o instanceof Array) ) throw new Error( 'Expected Array.' );
const t = CTSType["string"];
return o.map( t.nosj );
},
},
"L(string)": {
name: "L(string)",
json( o: any ) {
return o;
},
nosj( o: any ) {
if( o == null ) return undefined;
if( !(o instanceof Array) ) throw new Error( 'Expected Array.' );
const t = CTSType["string"];
return o.map( t.nosj );
},
},
"float": {
name: "float",
set( o: number ): number { o = Object( o ); (o as any)[SymCTS] = this; return o; },
json( o: any ) {return o; },
nosj( o: any ) {return o == null ? undefined : o; },
},
}
export const SymCTS = Symbol.for("CK.CTSType");
