import { ICommandModel, ICommand } from "../../../Cris/Model";
import { CTSType } from "../../../Core/CTSType";

export class SliderCommand implements ICommand {
public sliderValue: number;
public constructor()
public constructor(
sliderValue?: number)
constructor(
sliderValue?: number)
{
this.sliderValue = sliderValue ?? 0;
CTSType["CK.Observable.ServerSample.App.ISliderCommand"].set( this );
}

get commandModel(): ICommandModel { return SliderCommand.#m; }

static #m =  {
applyAmbientValues( command: any, a: any, o: any ) {
// This command has no AmbientValue property.

}

}
readonly _brand!: ICommand["_brand"] & {"9":any};
}
