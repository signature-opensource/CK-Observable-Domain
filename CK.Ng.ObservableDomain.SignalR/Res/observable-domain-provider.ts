import { ObservableDomainClient } from '@local/ck-gen/CK/ObservableDomain/ObservableDomainClient';
import { SignalRObservableLeagueDomainService } from '@local/ck-gen/CK/Observable/SignalRWatcher/SignalRObservableLeagueDomainService';
import { HttpCrisEndpoint, IObservableDomainClientConfiguration } from '@local/ck-gen';
import { inject, InjectionToken } from '@angular/core';

export const OBSERVABLE_DOMAIN_CLIENT_CONFIGURATION = new InjectionToken<IObservableDomainClientConfiguration>('IObservableDomainClientConfiguration');

export const initializeObservableDomainClient = () => {
    const crisEndpoint = inject( HttpCrisEndpoint );
    const config = inject( OBSERVABLE_DOMAIN_CLIENT_CONFIGURATION, { optional: true } ) ?? undefined;
    const hubUrl = '/hub/league';
    console.log( `ObservableLeague Hub URL: ${hubUrl}` );
    const driver = new SignalRObservableLeagueDomainService( hubUrl, crisEndpoint );
    const odc = new ObservableDomainClient( driver, config );
    console.log( 'Starting ObservableDomainClient' );
    odc.start();
    return odc;
};
