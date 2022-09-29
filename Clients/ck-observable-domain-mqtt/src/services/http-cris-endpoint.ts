import { CommandModel, ICrisEndpoint, Command } from "@signature/generated/dist/esm/CK/Cris/Model";
import { AxiosInstance } from "axios";
export class HttpCrisEndpoint /*implements ICrisEndpoint*/ {
    constructor(private readonly axios: AxiosInstance) {
    }

    /*send<T>(command: Command<T>): Promise<ICommandResult<T>> {
        
        this.axios.post('', this.crisified(command) );
        return undefined;
    }
*/
    private crisified( command: { commandModel: CommandModel<any>; } ): any {
        let string = `["${command.commandModel.commandName}"`;
        string += `,${JSON.stringify( command, ( key, value ) => {
          return key == "commandModel" ? undefined : value;
        } )}]`
        return string;
      }
}
