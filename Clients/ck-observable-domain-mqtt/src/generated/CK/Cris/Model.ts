
export interface CommandModel<TResult> {
    readonly commandName: string;
    readonly isFireAndForget: boolean;
    send: (e: ICrisEndpoint) => Promise<TResult>;
    applyAmbientValues: (values: { [index: string]: any }, force?: boolean ) => void;
}

type CommandResult<T> = T extends { commandModel: CommandModel<infer TResult> } ? TResult : never;

export interface ICrisEndpoint {
    send<T>(command: T): Promise<CommandResult<T>>;
}
