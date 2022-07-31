import { IPoco } from "../generated/CK/Core/IPoco";
import { CommandModel, ICrisEndpoint } from "../generated/CK/Cris/Model";
import { AxiosInstance } from "axios";
export class HttpCrisEndpoint {
    constructor(private readonly axios: AxiosInstance) {
    }

    send<T>(command: { commandModel: CommandModel<T> }): Promise<T> {
        
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