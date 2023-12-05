import { UserMessageLevel } from "../Core/UserMessageLevel";
import { SimpleUserMessage } from "../Core/SimpleUserMessage";
import { AmbientValues } from "./AmbientValues/AmbientValues";
import { AmbientValuesCollectCommand } from "./AmbientValues/AmbientValuesCollectCommand";
import {ICommand,ExecutedCommand,CrisError} from './Model';
import { AmbientValuesOverride } from './AmbientValues/AmbientValuesOverride';

/**
 * Abstract Cris endpoint. 
 * The doSendAsync protected method must be implemented.
 */
export abstract class CrisEndpoint
{
    private _ambientValuesRequest: Promise<AmbientValues>|undefined;
    private _ambientValues: AmbientValues|undefined;
    private _subscribers: Set<( eventSource: CrisEndpoint ) => void>;
    private _isConnected: boolean;

    constructor()
    {
        this.ambientValuesOverride = new AmbientValuesOverride();
        this._isConnected = false;
        this._subscribers = new Set<() => void>();
    }

    /**
    * Enables ambient values to be overridden.
    * Sensible ambient values (like the actorId when CK.Cris.Auth is used) are checked against
    * secured contextual values: overriding them will trigger a ValidationError. 
    **/    
    public readonly ambientValuesOverride: AmbientValuesOverride;


    //#region isConnected
    /** Gets whether this HttpEndpointService is connected: the last command sent  
     *  has been handled by the server. 
     **/
    public get isConnected(): boolean { return this._isConnected; }

    /**
     * Registers a callback function that will be called when isConnected changed.
     * @param func A callback function.
     */
    public addOnIsConnectedChanged( func: ( eventSource: CrisEndpoint ) => void ): void 
    {
        if( func ) this._subscribers.add( func );
    }

    /**
     * Unregister a previously registered callback.
     * @param func The callback function to remove.
     * @returns True if the callback has been found and removed, false otherwise.
     */
    public removeOnIsConnectedChange( func: ( eventSource: CrisEndpoint ) => void ): boolean {
        return this._subscribers.delete( func );
    }

    /**
     * Sets whether this endpoint is connected or not. When setting false, this triggers
     * an update of the ambient values that will run until success and eventually set
     * a true isConnected back.
     * @param value Whether the connection mus be considered available or not.
     */
    protected setIsConnected( value: boolean ): void 
    {
        if( this._isConnected !== value )
        {
            this._isConnected = value;
            if( !value ) 
            {
                this.updateAmbientValuesAsync();
            }
            this._subscribers.forEach( func => func( this ) );
        }
    }

    //#endregion

    /**
    * Sends a AmbienValuesCollectCommand and waits for its return.
    * Next commands will wait for the ambient values to be received before being sent.
    **/    
    public updateAmbientValuesAsync() : Promise<AmbientValues>
    {
        if( this._ambientValuesRequest ) return this._ambientValuesRequest;
        this._ambientValues = undefined;
        return this._ambientValuesRequest = this.waitForAmbientValuesAsync();
    }

    /**
    * Sends a command and returns an ExecutedCommand with the command's result or a CrisError.
    **/    
    public async sendAsync<T>(command: ICommand<T>): Promise<ExecutedCommand<T>>
    {
        let a = this._ambientValues;
        // Don't use coalesce here since there may be no ambient values (an empty object is truthy).
        if( a === undefined ) a = await this.updateAmbientValuesAsync();
        command.commandModel.applyAmbientValues( command, a, this.ambientValuesOverride );
        return await this.doSendAsync( command ); 
    }

    /**
    * Sends a command and returns the command's result or throws a CrisError.
    **/    
    public async sendOrThrowAsync<T>( command: ICommand<T> ): Promise<T>
    {
        const r = await this.sendAsync( command );
        if( r.result instanceof CrisError ) throw r.result;
        return r.result;
    }

    /**
     * Core method to implement. Can use the handleJsonResponse helper to create 
     * the final ExecutedCommand<T> from a Json object response. 
     * @param command The command to send.
     * @returns The resulting ExecutedCommand<T>.
     */
    protected abstract doSendAsync<T>(command: ICommand<T>): Promise<ExecutedCommand<T>>;

    /**
     * Available helper.
     * @param command The sent command.
     * @param data The Json object response.
     * @returns The resulting ExecutedCommand<T>.
     */
    protected handleJsonResponse<T>( command: ICommand<T>, data: any ) : ExecutedCommand<T>
    {
        if( data.correlationId !== undefined )
        {
           if( ["boolean","string","number","boolean"].includes( typeof(data.result) ) )
           {
              return {command: command, result: data.result as T, correlationId: data.correlationId };
           }
           else if( data.result instanceof Array && data.result.length == 2 )
           {
               if( data.result[0] === "AspNetCrisResultError" )
               {
                      // Normalized null or empty to undefined.
                      data.correlationId = data.correlationId ? data.correlationId : undefined;
                      const e = data.result[1] as {isValidationError?: boolean, logKey?: string, messages?: ReadonlyArray<[UserMessageLevel,string,number]>};
                      if( typeof e.isValidationError === "boolean" && e.messages instanceof Array )
                      {
                          const messages: Array<SimpleUserMessage> = [];
                          for( const msg of e.messages )
                          {
                              if(!(msg instanceof Array && msg.length == 3)) return invalidResponse(command,e.logKey);
                              // silently skip potential [0] or other incomplete arrays.
                              if(msg.length == 3)
                              {
                                  messages.push( new SimpleUserMessage(msg[0],msg[1],msg[2]) );
                              }
                          }
                          const m = messages.find( m => m.level === UserMessageLevel.Error ) 
                                      ?? messages.find( m => m.level === UserMessageLevel.Warn )
                                      ?? messages.find( m => m.level === UserMessageLevel.Info );
                          const message = m && m.message ? m.message : 'Error (missing Cris error message)';
                          return {command: command, result: new CrisError(command,message,e.isValidationError,undefined,messages,e.logKey), correlationId: data.correlationId };
                      }
                      return invalidResponse( command, data.correlationId );
               }
               else if( data.result[0] === "SimpleUserMessage" )
               {
                   const msg = data.result[1];
                   if(!(msg instanceof Array && msg.length === 3)) return invalidResponse(command, data.correlationId);
                   return {command: command, result: new SimpleUserMessage(msg[0],msg[1],msg[2]) as T, correlationId: data.correlationId};
               }
               return {command: command, result: data.result[1] as T, correlationId: data.correlationId };
           }
        }
        return invalidResponse( command );

        function invalidResponse( cmd: ICommand<unknown>, cId?: string ) 
        {
            const m = 'Invalid command response.';
            return {command: cmd, result: new CrisError(cmd, m, false, new Error(m)), correlationId: cId};
        } 
    }

    private async waitForAmbientValuesAsync() : Promise<AmbientValues>
    {
        while(true)
        {
            var e = await this.doSendAsync( AmbientValuesCollectCommand.create() );
            if( e.result instanceof CrisError )
            {
                console.error( "Error while getting AmbientValues. Retrying.", e.result );
                this.setIsConnected( false );
            }
            else
            {
                this._ambientValuesRequest = undefined;
                this._ambientValues = <AmbientValues>e.result;
                this.setIsConnected( true );
                return this._ambientValues;
            }
        }
    }
}