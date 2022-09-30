import { CommandModel, ICrisEndpoint, Command, ICommandResult } from "@signature/generated";
import { AxiosInstance } from "axios";
export class HttpCrisEndpoint implements ICrisEndpoint {
    constructor(private readonly axios: AxiosInstance) {
    }

    async send<T>(command: Command<T>): Promise<ICommandResult<T>> {
        
       const resp = await this.axios.post('', this.crisified(command) );
       if(resp.status != 200) {
        
       }
    }

    private crisified( command: { commandModel: CommandModel<any>; } ): any {
        let string = `["${command.commandModel.commandName}"`;
        string += `,${JSON.stringify( command, ( key, value ) => {
          return key == "commandModel" ? undefined : value;
        } )}]`
        return string;
      }
}
