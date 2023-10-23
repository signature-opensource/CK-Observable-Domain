import { UserMessageLevel } from "./UserMessageLevel";


/**
 * Simple info, warn or error message with an optional indentation.
 **/
export class SimpleUserMessage
{
    /**
     * @param message Message level (info, warn or error). 
     * @param message Message text. 
     * @param message Optional indentation. 
    **/
    constructor(
        public readonly level: UserMessageLevel,
        public readonly message: string,
        public readonly depth: number = 0
    ) {}

    toString() {
        return '['+UserMessageLevel[this.level]+'] ' + this.message;
      }
}
