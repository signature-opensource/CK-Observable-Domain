
export interface CommandModel<TResult> {
    readonly commandName: string;
    readonly isFireAndForget: boolean;
    send: (e: ICrisEndpoint) => Promise<TResult>;
    applyAmbientValues: (values: { [index: string]: any }, force?: boolean ) => void;
}

export interface Command<TResult = void> {
  commandModel: CommandModel<TResult>;
}

export type CommandResult<TResult> = TResult extends Command<infer TResult> ? TResult : never;

export interface ICrisEndpoint {
 send<T>(command: Command<T>): Promise<T>;
}
