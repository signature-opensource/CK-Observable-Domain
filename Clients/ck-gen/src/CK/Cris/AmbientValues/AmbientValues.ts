import { IPoco } from "../../Core/IPoco";
import { CTSType } from "../../Core/CTSType";

export class AmbientValues implements IPoco {
public currentCultureName: string;
public actorId: number;
public actualActorId: number;
public deviceId: string;
public constructor()
public constructor(
currentCultureName?: string,
actorId?: number,
actualActorId?: number,
deviceId?: string)
constructor(
currentCultureName?: string,
actorId?: number,
actualActorId?: number,
deviceId?: string)
{
this.currentCultureName = currentCultureName ?? "";
this.actorId = actorId ?? 0;
this.actualActorId = actualActorId ?? 0;
this.deviceId = deviceId ?? "";
CTSType["CK.Cris.AmbientValues.IAmbientValues"].set( this );
}
readonly _brand!: IPoco["_brand"] & {"0":any};
}
