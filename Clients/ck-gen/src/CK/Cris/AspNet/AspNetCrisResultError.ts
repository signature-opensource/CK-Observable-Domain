import { IPoco } from "../../Core/IPoco";
import { CTSType } from "../../Core/CTSType";

export class AspNetCrisResultError implements IPoco {
public isValidationError: boolean;
public readonly errors: Array<string>;
public logKey?: string;
public constructor()
public constructor(
isValidationError?: boolean,
errors?: Array<string>,
logKey?: string)
constructor(
isValidationError?: boolean,
errors?: Array<string>,
logKey?: string)
{
this.isValidationError = isValidationError ?? false;
this.errors = errors ?? [];
this.logKey = logKey;
CTSType["AspNetCrisResultError"].set( this );
}
readonly _brand!: IPoco["_brand"] & {"7":any};
}
