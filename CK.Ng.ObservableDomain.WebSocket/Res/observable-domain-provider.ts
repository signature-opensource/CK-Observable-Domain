import { inject, InjectionToken } from '@angular/core';
import { HttpCrisEndpoint, WebSocketObservableDomainConnection, ObservableDomainClient, IObservableDomainClientConfiguration } from '@local/ck-gen';

export const OBSERVABLE_DOMAIN_CLIENT_CONFIGURATION = new InjectionToken<IObservableDomainClientConfiguration>('IObservableDomainClientConfiguration');

export const initializeObservableDomainClient= () => {
    const crisEndpoint = inject( HttpCrisEndpoint );
    const config = inject( OBSERVABLE_DOMAIN_CLIENT_CONFIGURATION, { optional: true } ) ?? undefined;
    const hubUrl = '/ws/observable';
    console.debug( `ObservableDomainDriver URL: ${hubUrl}.`);
    const connection = new WebSocketObservableDomainConnection( hubUrl, crisEndpoint );
    const odc = new ObservableDomainClient( connection, config );
    console.info( 'Starting ObservableDomainClient.');
    odc.start();
    return odc;
}
