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
      updates$.subscribe( () => {
        const singleton = this.#findSingleton( domain?.allObjects );
        if( singleton !== undefined ) this.#sampleSingleton().slider.set( singleton.slider );
      } );
    } );
  }

  #findSingleton( objects: Iterable<unknown> | undefined ): __SampleSingleton | undefined {
    if (!objects) {
      return undefined;
    }

    for (const o of objects) {
      if (this.#isSampleSingleton(o)) {
        return o;
      }
    }

    return undefined;
  }

  #isSampleSingleton(o: unknown): o is __SampleSingleton {
    return (o as __SampleSingleton).slider !== undefined;
  }
}
