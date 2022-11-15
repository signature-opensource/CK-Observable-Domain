import { CommandModel, ICrisEndpoint } from "../Model";
import { AmbientValues } from "./AmbientValues";
import { SymbolType } from "../../Core/IPoco";

export class AmbientValuesCollectCommand {
readonly commandModel: CommandModel<AmbientValues|undefined> =  {
commandName: "CK.Cris.AmbientValues.IAmbientValuesCollectCommand",
isFireAndForget: false,
applyAmbientValues: (values: { [index: string]: any }, force?: boolean ) =>  {
// This command has no property that appear in the Ambient Values.

}

};

[SymbolType] = "CK.Cris.AmbientValues.IAmbientValuesCollectCommand";
/**
 * This SHOULD NOT be called! It's unfortunately public (waiting for the Default Values issue to be solved).
 **/
constructor() {

}
/**
 * Factory method that exposes all the properties as parameters.
 **/
static create(  ) : AmbientValuesCollectCommand

/**
 * Creates a new command and calls a configurator for it.
 * @param config A function that configures the new command.
 **/
static create( config: (c: AmbientValuesCollectCommand) => void ) : AmbientValuesCollectCommand
// Implementation.
static create( config?: (c: AmbientValuesCollectCommand) => void ) : AmbientValuesCollectCommand
 {
const c = new AmbientValuesCollectCommand();
if( config ) config(c);
return c;
}
}
