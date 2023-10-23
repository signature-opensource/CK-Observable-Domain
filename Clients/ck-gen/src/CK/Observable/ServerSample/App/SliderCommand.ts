import { CommandModel } from "../../../Cris/Model";
import { SymbolType } from "../../../Core/IPoco";

export class SliderCommand {
get commandModel() { return SliderCommand._commandModel; }
private static readonly _commandModel: CommandModel<void> =  {
commandName: "CK.Observable.ServerSample.App.ISliderCommand",
applyAmbientValues( command: any, a: any, o: any ) {
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
