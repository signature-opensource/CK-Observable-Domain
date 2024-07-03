import { ICommandModel, ICommand } from "../Model";
import { AmbientValues } from "./AmbientValues";
import { CTSType } from "../../Core/CTSType";

export class AmbientValuesCollectCommand implements ICommand<AmbientValues|undefined> {
public constructor()
public constructor()
constructor()
{
CTSType["CK.Cris.AmbientValues.IAmbientValuesCollectCommand"].set( this );
}

get commandModel(): ICommandModel { return AmbientValuesCollectCommand.#m; }

static #m =  {
applyAmbientValues( command: any, a: any, o: any ) {
// This command has no AmbientValue property.

}

}
readonly _brand!: ICommand<AmbientValues|undefined>["_brand"] & {"5":any};
}
