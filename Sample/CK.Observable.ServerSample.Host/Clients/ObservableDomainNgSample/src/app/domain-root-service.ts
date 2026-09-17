import {inject, Injectable, Signal, signal, WritableSignal} from '@angular/core';
import {ObservableDomainClient} from '@local/ck-gen/CK/ObservableDomain/ObservableDomainClient';

type ProjectSignals<T> =
  T extends WritableSignal<infer U> ? Signal<ProjectSignals<U>> :
    T extends Signal<infer U> ? Signal<ProjectSignals<U>> :
      T extends readonly (infer E)[] ? readonly ProjectSignals<E>[] :
        T extends (infer E)[] ? ProjectSignals<E>[] :
          T extends object ? { [K in keyof T]: ProjectSignals<T[K]> } :
            T;

interface __SampleSingleton { slider: number; }
type SampleSingleton = WritableSignal<{ slider: WritableSignal<number> }>;

@Injectable( {
  providedIn: 'root'
} )
export class DomainRootService {
  readonly #odClient = inject( ObservableDomainClient );

  readonly #sampleSingleton: SampleSingleton = signal( { slider: signal( 0 )} );
  public get sampleSingleton(): ProjectSignals<SampleSingleton> { return this.#sampleSingleton; }

  constructor() {
    const domainName: string = 'Test-Domain';
    this.#odClient.listenToDomainAsync( domainName ).then( ( updates$ ) => {
      const domain = this.#odClient.getDomain( domainName );
      updates$.subscribe(() => {
        const singleton = domain?.singletons?.get('CK.Observable.ServerSample.App.SampleSingleton') as __SampleSingleton;
        if( singleton !== undefined ) this.#sampleSingleton().slider.set( singleton.slider );
      } );
    } );
  }
}
