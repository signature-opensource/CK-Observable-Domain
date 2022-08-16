import { CommandModel, ICrisEndpoint, CommandResult, Command } from "@signature/generated/CK/Cris/Model";
import { AxiosInstance } from "axios";
export class HttpCrisEndpoint implements ICrisEndpoint {
    constructor(private readonly axios: AxiosInstance) {
    }

    send<T>(command: T): Promise<CommandResult<T>> {
        
        this.axios.post('', this.crisified(command) );
        return undefined;
    }

    private crisified( command: { commandModel: CommandModel<any>; } ): any {
        let string = `["${command.commandModel.commandName}"`;
        string += `,${JSON.stringify( command, ( key, value ) => {
          return key == "commandModel" ? undefined : value;
        } )}]`
        return string;
      }
}