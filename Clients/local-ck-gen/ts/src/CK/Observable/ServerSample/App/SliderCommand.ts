import { CommandModel, ICrisEndpoint } from "../../../Cris/Model";
import { SymbolType } from "../../../Core/IPoco";

export class SliderCommand {
readonly commandModel: CommandModel<void> =  {
commandName: "CK.Observable.ServerSample.App.ISliderCommand",
isFireAndForget: false,
applyAmbientValues: (values: { [index: string]: any }, force?: boolean ) =>  {
// This command has no property that appear in the Ambient Values.

}

};
sliderValue: number;

[SymbolType] = "CK.Observable.ServerSample.App.ISliderCommand";
/**
 * This SHOULD NOT be called! It's unfortunately public (waiting for the Default Values issue to be solved).
 **/
constructor() {
this.sliderValue = undefined!;

}
/**
 * Factory method that exposes all the properties as parameters.
 **/
static create( sliderValue: number ) : SliderCommand

/**
 * Creates a new command and calls a configurator for it.
 * @param config A function that configures the new command.
 **/
static create( config: (c: SliderCommand) => void ) : SliderCommand
// Implementation.
static create( sliderValue: number|((c:SliderCommand) => void) ) {
if( typeof sliderValue === 'function' ) {
const c = new SliderCommand();
sliderValue(c);
return c;
}
const c = new SliderCommand();
c.sliderValue = sliderValue;
return c;
}
}
