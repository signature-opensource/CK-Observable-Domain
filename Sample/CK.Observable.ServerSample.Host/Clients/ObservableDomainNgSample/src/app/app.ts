import {Component, computed, inject, signal} from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { CKGenAppModule } from '@local/ck-gen/CK/Angular/CKGenAppModule';
import { HttpCrisEndpoint } from '@local/ck-gen/CK/Cris/HttpCrisEndpoint';
import { SliderCommand } from '@local/ck-gen/CK/Observable/ServerSample/App/SliderCommand';
import { MultiEventCommand } from '@local/ck-gen/CK/Observable/ServerSample/App/MultiEventCommand';
import { DomainRootService } from './domain-root-service';

@Component( {
  selector: 'app-root',
  imports: [RouterOutlet, CKGenAppModule],
  templateUrl: './app.html',
  styleUrl: './app.less'
} )
export class App {
  #crisEndpoint = inject( HttpCrisEndpoint );
  #domain = inject( DomainRootService );

  protected readonly title = signal( 'ObservableDomainNgSample' );
  protected readonly sliderValue = computed( () => this.#domain.sampleSingleton().slider() ?? 0 );
  protected readonly multiEventCounter = signal( 0 );

  async sliderUpdate( sliderValue: string ): Promise<void> {
    await this.#crisEndpoint.sendOrThrowAsync<void>( new SliderCommand( parseFloat( sliderValue ) ) );
  }

  async sendMultiEvent(): Promise<void> {
    const c = this.multiEventCounter() + 1;
    this.multiEventCounter.set( c );
    await this.#crisEndpoint.sendOrThrowAsync<void>( new MultiEventCommand( c * 10, `item-${c}`, c ) );
  }
}

