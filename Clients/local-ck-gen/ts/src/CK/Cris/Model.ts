import { Axios, AxiosHeaders, RawAxiosRequestConfig } from "axios";
import { VESACode } from "./VESACode";
import { CrisResultError } from "./CrisResultError";
import { CrisResult } from "./CrisResult";



export type ICommandResult<T> = {
    code: VESACode.Error | VESACode.ValidationError,
    result: CrisResultError,
    correlationId?: string
} |
{
    code: 'CommunicationError',
    result: Error
} |
{
    code: VESACode.Synchronous,
    result: T,
    correlationId?: string
};

export interface CommandModel<TResult> {
    readonly commandName: string;
    readonly isFireAndForget: boolean;
    applyAmbientValues: (values: { [index: string]: any }, force?: boolean ) => void;
}

export interface Command<TResult = void> {
  commandModel: CommandModel<TResult>;
}

export interface ICrisEndpoint {
  send<T>(command: Command<T>): Promise<ICommandResult<T>>;
  sendOrThrow<T>( command: Command<T> ): Promise<ICommandResult<T>>;
}

export class CrisError extends Error {
  public readonly command: Command<unknown>;
  public readonly result?: ICommandResult<unknown>;

  constructor( message: string, command: Command<unknown>, result?: ICommandResult<unknown> ) {
    super( message );
    this.command = command;
    this.result = result;
  }
}

export function findCrisErrorMessage( innerResult: any ): string {
  if ( Array.isArray( innerResult ) && innerResult.length > 1 ) {
    if ( innerResult[0] === 'CrisResultError' ) {
      const p = innerResult[1];
      if ( p.message ) return p.message;
      if ( p.errors && Array.isArray( p.errors ) ) {
        return p.errors.join( '; ' );
      }
    }
  }
  return JSON.stringify( innerResult );
}

const defaultCrisAxiosConfig: RawAxiosRequestConfig = {
  responseType: 'text',
  headers: {
    common: new AxiosHeaders({
      'Content-Type': 'application/json'
    })
  }
};

export class HttpCrisEndpoint implements ICrisEndpoint {
  public axiosConfig: RawAxiosRequestConfig; // Allow user replace

  constructor(private readonly axios: Axios, private readonly crisEndpointUrl: string) {
    this.axiosConfig = defaultCrisAxiosConfig;
  }

  async send<T>(command: Command<T>): Promise<ICommandResult<T>> {
    try {
      let string = `["${command.commandModel.commandName}"`;
      string += `,${JSON.stringify(command, (key, value) => {
        return key == "commandModel" ? undefined : value;
      })}]`;
      const resp = await this.axios.post<string>(this.crisEndpointUrl, string, this.axiosConfig);

      const result = JSON.parse(resp.data)[1] as CrisResult; // TODO: @Dan implement io-ts.
      if (result.code == VESACode.Synchronous) {
        return {
          code: VESACode.Synchronous,
          result: result.result as T,
          correlationId: result.correlationId
        };
      }
      else if (result.code == VESACode.Error || result.code == VESACode.ValidationError) {
        return {
          code: result.code as VESACode.Error | VESACode.ValidationError,
          result: result.result as CrisResultError,
          correlationId: result.correlationId
        };
      }
      else if (result.code == VESACode.Asynchronous) {
        throw new Error("Endpoint returned VESACode.Asynchronous which is not yet supported by this client.");
      } else {
        throw new Error("Endpoint returned an unknown VESA Code.");
      }
    } catch (e) {
      if (e instanceof Error) {
        return {
          code: 'CommunicationError',
          result: e
        };
      } else {
        return {
          code: 'CommunicationError',
          result: new Error(`Unknown error ${e}.`)
        };
      }
    }
  }

  async sendOrThrow<T>( command: Command<T> ): Promise<ICommandResult<T>> {
    const result = await this.send( command );
    if ( result.code === 'CommunicationError' ) {
      throw new CrisError(
        `CRIS communication error (${command.commandModel.commandName}): ${result.result.toString()}`,
        command,
        result,
      );
    } else if ( result.code == VESACode.Error ) {
      throw new CrisError(
        `CRIS error: ${findCrisErrorMessage( result.result )} (${command.commandModel.commandName})`,
        command,
        result,
      );
    } else if ( result.code == VESACode.ValidationError ) {
      throw new CrisError(
        `CRIS validation error: ${findCrisErrorMessage( result.result )} (${command.commandModel.commandName})`,
        command,
        result,
      );
    }

    return result;
  }
}
