import { inject, Injectable, signal, WritableSignal } from '@angular/core';
import { ObservableDomainClient } from '@local/ck-gen/CK/ObservableDomain/ObservableDomainClient';
import { ReplaySubject } from 'rxjs';

export interface ORoot {
  Slider: number;
}

@Injectable( {
  providedIn: 'root'
} )
export class DomainRootService {
  readonly #odClient = inject( ObservableDomainClient );

  public readonly domainName: string = 'Test-Domain';
  root: WritableSignal<ORoot | undefined> = signal( undefined );
  root$ = new ReplaySubject<ORoot>( 1 );

  constructor() {
    this.#odClient.listenToDomainAsync( this.domainName ).then( ( agentRoot$ ) => {
      agentRoot$.subscribe( ( x ) => {
        if ( x && x.length > 0 ) {
          console.debug( 'Domain update:', x[0] );
          this.root.set( x[0] );
          this.root$.next( x[0] );
          console.debug( 'Root update:', this.root() );
        }
      } );
    } );
  }
}
