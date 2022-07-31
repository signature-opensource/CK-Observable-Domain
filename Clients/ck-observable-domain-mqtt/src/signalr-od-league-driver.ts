import { WatchEvent } from '@signature-code/ck-observable-domain';
import { HttpTransportType, HubConnection, HubConnectionBuilder, HubConnectionState } from '@aspnet/signalr';
import { IObservableDomainLeagueDriver } from './iod-league-driver';

export class SignalRObservableLeagueDomainService implements IObservableDomainLeagueDriver {
  private readonly connection: HubConnection;
  constructor(hubUrl: string) {
    this.connection = new HubConnectionBuilder()
      .withUrl(hubUrl, HttpTransportType.None)
      .build();
    this.connection.start();
  }

  public start(): Promise<void> {
    return this.connection.start();
  }

  public onMessage(eventHandler: (domainName: string, eventsJson: WatchEvent) => void) {
    this.connection.on('OnStateEvents', (domainName, eventText) => {
      eventHandler(domainName, JSON.parse(eventText));
    });
  }

  public onClose(eventHandler: (error: Error) => void) {
    if (this.connection.state == HubConnectionState.Disconnected) {
      eventHandler(null);
      return;
    }
    this.connection.onclose(eventHandler);
  }

  public stop(): Promise<void> {
    return this.connection.stop();
  }
}
