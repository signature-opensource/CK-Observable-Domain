import { ObservableDomainClient } from '@local/ck-gen/CK/ObservableDomain/ObservableDomainClient';
import { SignalRObservableLeagueDomainService } from '@local/ck-gen/CK/Observable/SignalRWatcher/SignalRObservableLeagueDomainService';
import { HttpCrisEndpoint } from '@local/ck-gen';
import { inject } from '@angular/core';

export const initializeObservableDomainClient = () => {
    const crisEndpoint = inject( HttpCrisEndpoint );
    const hubUrl = '/hub/league';
    console.log( `ObservableLeague Hub URL: ${hubUrl}` );
    const driver = new SignalRObservableLeagueDomainService( hubUrl, crisEndpoint );
    const odc = new ObservableDomainClient( driver );
    console.log( 'Starting ObservableDomainClient' );
    odc.start();
    return odc;
};
