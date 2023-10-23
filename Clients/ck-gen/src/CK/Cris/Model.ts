import { SimpleUserMessage } from "../Core/SimpleUserMessage";
import { UserMessageLevel } from "../Core/UserMessageLevel";

/**
 * Describes a command. 
 **/
export type CommandModel<TResult> = {
    /**
     * Gets the name of the command. 
     **/
    readonly commandName: string;
    /**
     * This supports the CrisEdpoint implementation. This is not to be used directly.
     **/
    readonly applyAmbientValues: (command: any, a: any, o: any ) => void;
}

/** 
 * Command abstraction: command with or without a result. 
 * **/
export interface ICommand<TResult = void> { 
    /**
     * Gets the command description. 
     **/
    readonly commandModel: CommandModel<TResult>;
}

/** 
 * Captures the result of a command execution.
 **/
export type ExecutedCommand<T> = {
    /** The executed command. **/
    readonly command: ICommand<T>,
    /** The execution result. **/
    readonly result: CrisError | T,
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
     * Gets the messages. At least one message is guranteed to exist.
     */
    public readonly messages: ReadonlyArray<SimpleUserMessage>; 
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
                 message: string, 
                 isValidationError: boolean,
                 innerError?: Error, 
                 messages?: ReadonlyArray<SimpleUserMessage>,
                 logKey?: string ) 
    {
        super( message );
        this.command = command;   
        this.errorType = isValidationError 
                            ? "ValidationError" 
                            : innerError ? "CommunicationError" : "ExecutionError";
        this.innerError = innerError;
        this.messages = messages && messages.length > 0 
                        ? messages
                        : [new SimpleUserMessage(UserMessageLevel.Error,message,0)];
        this.logKey = logKey;
    }
}
