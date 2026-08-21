import { inject, InjectionToken } from '@angular/core';
import { HttpCrisEndpoint, WebSocketObservableDomainConnection, ObservableDomainClient, IObservableDomainClientConfiguration, WSConnection } from '@local/ck-gen';

export const OBSERVABLE_DOMAIN_CLIENT_CONFIGURATION = new InjectionToken<IObservableDomainClientConfiguration>('IObservableDomainClientConfiguration');

export const initializeObservableDomainClient= () => {
    const crisEndpoint = inject( HttpCrisEndpoint );
    // The socket is the application's, not ours: this only claims the 'OD' topic on it.
    const wsConnection = inject( WSConnection );
    const config = inject( OBSERVABLE_DOMAIN_CLIENT_CONFIGURATION, { optional: true } ) ?? undefined;
    const connection = new WebSocketObservableDomainConnection( wsConnection, crisEndpoint );
    const odc = new ObservableDomainClient( connection, config );
    console.info( 'Starting ObservableDomainClient.');
    odc.start();
    return odc;
}
