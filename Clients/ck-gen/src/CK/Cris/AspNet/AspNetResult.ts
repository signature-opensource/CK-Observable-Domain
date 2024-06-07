import { IPoco } from "../../Core/IPoco";
import { SimpleUserMessage } from "../../Core/SimpleUserMessage";
import { CTSType } from "../../Core/CTSType";

export class AspNetResult implements IPoco {
public result?: {};
public validationMessages?: Array<SimpleUserMessage>;
public correlationId?: string;
public constructor()
public constructor(
result?: {},
validationMessages?: Array<SimpleUserMessage>,
correlationId?: string)
constructor(
result?: {},
validationMessages?: Array<SimpleUserMessage>,
correlationId?: string)
{
this.result = result;
this.validationMessages = validationMessages;
this.correlationId = correlationId;
CTSType["AspNetResult"].set( this );
}
readonly _brand!: IPoco["_brand"] & {"6":any};
}
