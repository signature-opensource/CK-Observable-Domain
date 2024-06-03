import { AxiosInstance, AxiosHeaders, RawAxiosRequestConfig } from "axios";
import { ICommand, ExecutedCommand, CrisError } from "./Model";
import { CrisEndpoint } from "./CrisEndpoint";
import { AspNetResult } from "./AspNet/AspNetResult";
import { CTSType } from "../Core/CTSType";
import { AspNetCrisResultError } from "./AspNet/AspNetCrisResultError";

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
           const req = JSON.stringify(CTSType.toTypedJson(command));
           const resp = await this.axios.post(this.crisEndpointUrl, req, this.axiosConfig);
           const netResult = <AspNetResult>CTSType["AspNetResult"].nosj( JSON.parse(resp.data) );
           let r = netResult.result;
           if( r instanceof AspNetCrisResultError ) 
           {
               r = new CrisError(command, r.isValidationError, r.errors, undefined, netResult.validationMessages, r.logKey);
           }
           return {command: command, result: <T|CrisError>r, validationMessages: netResult.validationMessages, correlationId: netResult.correlationId };
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
           return {command, result: new CrisError(command, false, ["Communication error"], error )};                                              }
    }
}