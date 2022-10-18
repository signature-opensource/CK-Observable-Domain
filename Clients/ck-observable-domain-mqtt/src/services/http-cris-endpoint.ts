import { CommandModel, ICrisEndpoint, Command, ICommandResult, CrisResult, VESACode, CrisResultError } from "@signature/generated";
import { Axios, AxiosInstance } from "axios";
export class HttpCrisEndpoint implements ICrisEndpoint {
  constructor(private readonly axios: Axios) {
  }

  async send<T>(command: Command<T>): Promise<ICommandResult<T>> {
    try {
      let string = `["${command.commandModel.commandName}"`;
      string += `,${JSON.stringify(command, (key, value) => {
        return key == "commandModel" ? undefined : value;
      })}]`;
      const resp = await this.axios.post<string>('', string);
      
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
      }
      else {
        throw new Error("Endpoint returned an unknown VESA Code.");
      }
    } catch (e) {
      return {
        code: 'CommunicationError',
        result: e
      };
    }
  }
}
