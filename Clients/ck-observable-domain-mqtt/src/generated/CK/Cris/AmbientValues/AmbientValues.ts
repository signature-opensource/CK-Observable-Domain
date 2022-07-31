import { SymbolType } from '../../Core/IPoco';

export class AmbientValues {

[SymbolType]: 'CK.Cris.AmbientValues.IAmbientValues';
// This SHOULD NOT be called! It's unfortunately public (waiting for the Default Values issue to be solved).
/*private*/ constructor() {

}
/**
 * Factory method that exposes all the properties as parameters.
 **/
static create(  ) : AmbientValues

/**
 * Creates a new command and calls a configurator for it.
 * @param config A function that configures the new command.
 **/
static create( config: (c: AmbientValues) => void ) : AmbientValues
// Implementation.
static create( config?: (c: AmbientValues) => void ) : AmbientValues
 {
const c = new AmbientValues();
if( config ) config(c);
return c;
}
}
