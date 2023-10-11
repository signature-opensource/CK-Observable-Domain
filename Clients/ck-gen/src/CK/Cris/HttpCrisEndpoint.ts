import { AxiosInstance, AxiosHeaders, RawAxiosRequestConfig } from "axios";
import {CrisEndpoint} from './CrisEndpoint';
import {ICommand,ExecutedCommand,CrisError} from './Model';

const defaultCrisAxiosConfig: RawAxiosRequestConfig = {
    responseType: 'text',
    headers: {
      common: new AxiosHeaders({
        'Content-Type': 'application/json'
      })
    }
};

/**
 * Http Cris Command endpoint. 
 **/
export class HttpCrisEndpoint extends CrisEndpoint
{
    /**
     * Replaceable axios configuration.
     **/
    public axiosConfig: RawAxiosRequestConfig; 

    /**
     * Initializes a new HttpEndpoint that uses an Axios instance bound to a endpoint url.  
     * @param axios The axios instance.
     * @param crisEndpointUrl The Cris endpoint url to use.
     **/
    constructor(private readonly axios: AxiosInstance, private readonly crisEndpointUrl: string)
    {
        super();
        this.axiosConfig = defaultCrisAxiosConfig;
    }

    protected override async doSendAsync<T>(command: ICommand<T>): Promise<ExecutedCommand<T>>
    {
        try
        {
          let string = `["${command.commandModel.commandName}"`;
          string += `,${JSON.stringify(command, (key, value) => {
            return key == "commandModel" ? undefined : value;
          })}]`;
          const resp = await this.axios.post<string>(this.crisEndpointUrl, string, this.axiosConfig);

          return this.handleJsonResponse( command, JSON.parse(resp.data) );
        }
        catch( e )
        {
            var error : Error;
            if( e instanceof Error)
            {
                error = e;
            }
            else
            {
                // Error.cause is a mess. Log it.
                console.error( e );
                error = new Error(`Unhandled error ${e}.`);
            }
            this.setIsConnected(false);
            return {command, result: new CrisError(command,"Communication error", false, error )};
        }

    }

}