import { SymbolType } from "../Core/IPoco";

export class CrisResultError {
readonly errors: Array<string>;

[SymbolType] = "CrisResultError";
/**
 * Initializes a new CrisResultError with initial values for the readonly properties.
 * Use one of the create methods to set all properties.
 **/
// This SHOULD NOT be called! It's unfortunately public (waiting for the Default Values issue to be solved).
/*private*/ constructor( errors?: Array<string>)

/**
 * Initializes a new empty CrisResultError.
 **/
constructor()
// Implementation.
constructor( errors?: Array<string>)  {
this.errors = typeof errors === "undefined" ? new Array<string>() : errors;

}
/**
 * Factory method that exposes all the properties as parameters.
 * @param errors (Optional, automatically instantiated.) 
 **/
static create( errors?: Array<string> ) : CrisResultError

/**
 * Creates a new command and calls a configurator for it.
 * @param config A function that configures the new command.
 **/
static create( config: (c: CrisResultError) => void ) : CrisResultError
// Implementation.
static create( errors?: Array<string>|((c:CrisResultError) => void) ) {
if( typeof errors === 'function' ) {
const c = new CrisResultError();
errors(c);
return c;
}
const c = new CrisResultError(errors);

return c;
}
}
