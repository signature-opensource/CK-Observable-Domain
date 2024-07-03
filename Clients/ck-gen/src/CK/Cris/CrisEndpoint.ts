import { ICommand, ExecutedCommand, CrisError } from "./Model";
import { AmbientValues } from "./AmbientValues/AmbientValues";
import { AmbientValuesCollectCommand } from "./AmbientValues/AmbientValuesCollectCommand";
import { AmbientValuesOverride } from "./AmbientValues/AmbientValuesOverride";

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
    * Sensible ambient values (like the actorId when CK.IO.Auth is used) are checked against
    * secured contextual values: overriding them will trigger a ValidationError. 
    **/    
    public readonly ambientValuesOverride: AmbientValuesOverride;


    //#region isConnected
    /** 
    * Gets whether this HttpEndpointService is connected: the last command sent
    * has been handled by the server. 
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
    * an update of the endpoint values that will run until success and eventually set
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
    * Sends a AmbientValuesCollectCommand and waits for its return.
    * Next commands will wait for the ubiquitous values to be received before being sent.
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

    private async waitForAmbientValuesAsync() : Promise<AmbientValues>
    {
        while(true)
        {
            var e = await this.doSendAsync( new AmbientValuesCollectCommand() );
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