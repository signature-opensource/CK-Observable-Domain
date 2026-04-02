import { inject } from '@angular/core';
import { HttpCrisEndpoint, WebSocketObservableDomainConnection, ObservableDomainClient } from '@local/ck-gen';

export const initializeObservableDomainClient= () => {
    const crisEndpoint = inject( HttpCrisEndpoint );
    const hubUrl = '/ws/observable';
    console.debug( `ObservableDomainDriver URL: ${hubUrl}.`);
    const connection = new WebSocketObservableDomainConnection( hubUrl, crisEndpoint );
    const odc = new ObservableDomainClient( connection );
    console.info( 'Starting ObservableDomainClient.');
    odc.start();
    return odc;
}
