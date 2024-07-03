import { SimpleUserMessage } from "../Core/SimpleUserMessage";
import { UserMessageLevel } from "../Core/UserMessageLevel";
import { IPoco } from "../Core/IPoco";

/**
 * Describes a Command type. 
 **/
export interface ICommandModel {
    /**
     * This supports the CrisEndpoint implementation. This is not to be used directly.
     **/
    readonly applyAmbientValues: (command: any, a: any, o: any ) => void;
}

/** 
 * Abstraction of any Cris objects (currently only commands).
 **/
export interface ICrisPoco extends IPoco
{
    readonly _brand: IPoco["_brand"] & {"ICrisPoco": any};
}

/** 
 * Command abstraction.
 **/
export interface IAbstractCommand extends ICrisPoco
{
    /** 
     * Gets the command model.
     **/
    get commandModel(): ICommandModel;

    readonly _brand: ICrisPoco["_brand"] & {"ICommand": any};
}

/** 
 * Command with or without a result.
 * The C# ICommand (without result) is the TypeScript ICommand<void>.
 **/
export interface ICommand<out TResult = void> extends IAbstractCommand {
    readonly _brand: IAbstractCommand["_brand"] & {"ICommandResult": void extends TResult ? any : TResult};
}
                                

/** 
 * Captures the result of a command execution.
 **/
export type ExecutedCommand<T> = {
    /** The executed command. **/
    readonly command: ICommand<T>,
    /** The execution result. **/
    readonly result: CrisError | T,
    /**
     * An optional list of UserMessageLevel.info, UserMessageLevel.warn or UserMessageLevel.error
     * messages issued by the validation of the command: there can be info or warn messages even if the 
     * command has been succesfully executed. 
     * Validation error messages also appear in the CrisError.messages.
     **/
    readonly validationMessages?: Array<SimpleUserMessage>;
    /** Optional correlation identifier. **/
    readonly correlationId?: string
};

/**
 * Captures communication, validation or execution error.
 **/
export class CrisError extends Error {
   /**
    * Get this error type.
    */
    public readonly errorType : "CommunicationError"|"ValidationError"|"ExecutionError";
    /**
     * Gets the errors. At least one error is guaranteed to exist.
     */
    public readonly errors: ReadonlyArray<string>; 
    /**
     * Gets the validationMessages if any.
     */
    public readonly validationMessages?: ReadonlyArray<SimpleUserMessage>; 
    /**
     * The Error.cause support is a mess. This replaces it at this level. 
     */
    public readonly innerError?: Error; 
    /**
     * When defined, enables to find the backend log entry.
     */
    public readonly logKey?: string; 
    /**
     * Gets the command that failed.
     */
    public readonly command: ICommand<unknown>;

    constructor( command: ICommand<unknown>, 
                 isValidationError: boolean,
                 errors: ReadonlyArray<string>, 
                 innerError?: Error,
                 validationMessages?: ReadonlyArray<SimpleUserMessage>,
                 logKey?: string ) 
    {
        super( errors[0] );
        this.command = command;   
        this.errorType = isValidationError 
                            ? "ValidationError" 
                            : innerError ? "CommunicationError" : "ExecutionError";
        this.innerError = innerError;
        this.errors = errors;
        this.validationMessages = validationMessages;
        this.logKey = logKey;
    }
}
