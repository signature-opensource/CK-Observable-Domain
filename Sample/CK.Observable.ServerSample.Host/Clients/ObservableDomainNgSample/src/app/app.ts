import { Component, inject, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { CKGenAppModule } from '@local/ck-gen/CK/Angular/CKGenAppModule';
import { HttpCrisEndpoint } from '@local/ck-gen/CK/Cris/HttpCrisEndpoint';
import { SliderCommand } from '@local/ck-gen/CK/Observable/ServerSample/App/SliderCommand';
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

  root = this.#domain.root;
  constructor() {
  }

  async sliderUpdate( sliderValue: string ): Promise<void> {
    await this.#crisEndpoint.sendOrThrowAsync<void>( new SliderCommand( parseFloat( sliderValue ) ) );
  }
}

