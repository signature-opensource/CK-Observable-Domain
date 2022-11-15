import { VESACode } from "./VESACode";
import { SymbolType } from "../Core/IPoco";

export class CrisResult {
code: VESACode;
result?: unknown;
correlationId?: string;

[SymbolType] = "CrisResult";
/**
 * This SHOULD NOT be called! It's unfortunately public (waiting for the Default Values issue to be solved).
 **/
constructor() {
this.code = undefined!;
this.result = undefined!;
this.correlationId = undefined!;

}
/**
 * Factory method that exposes all the properties as parameters.
 * @param result (Optional, defaults to undefined) 
 * @param correlationId (Optional, defaults to undefined) 
 **/
static create( 
code: VESACode,
result?: unknown,
correlationId?: string ) : CrisResult

/**
 * Creates a new command and calls a configurator for it.
 * @param config A function that configures the new command.
 **/
static create( config: (c: CrisResult) => void ) : CrisResult
// Implementation.
static create( 
code: VESACode|((c:CrisResult) => void),
result?: unknown,
correlationId?: string ) {
if( typeof code === 'function' ) {
const c = new CrisResult();
code(c);
return c;
}
const c = new CrisResult();
c.code = code;
if( typeof result !== "undefined" ) c.result = result;
if( typeof correlationId !== "undefined" ) c.correlationId = correlationId;
return c;
}
}
