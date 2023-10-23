import { SymbolType } from "../../Core/IPoco";

export class AmbientValues {
actorId: number;
actualActorId: number;
deviceId: string;

[SymbolType] = "CK.Cris.AmbientValues.IAmbientValues";
/**
 * This SHOULD NOT be called! It's unfortunately public (waiting for the Default Values issue to be solved).
 **/
constructor() {
this.actorId = undefined!;
this.actualActorId = undefined!;
this.deviceId = undefined!;

}
/**
 * Factory method that exposes all the properties as parameters.
 **/
static create( 
actorId: number,
actualActorId: number,
deviceId: string ) : AmbientValues

/**
 * Creates a new command and calls a configurator for it.
 * @param config A function that configures the new command.
 **/
static create( config: (c: AmbientValues) => void ) : AmbientValues
// Implementation.
static create( 
actorId: number|((c:AmbientValues) => void),
actualActorId?: number,
deviceId?: string ) {
if( typeof actorId === 'function' ) {
const c = new AmbientValues();
actorId(c);
return c;
}
const c = new AmbientValues();
c.actorId = actorId;
c.actualActorId = actualActorId!;
c.deviceId = deviceId!;
return c;
}
}
