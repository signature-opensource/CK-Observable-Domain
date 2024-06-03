import { WatchEvent, IObservableDomainLeagueDriver } from '@signature/ck-observable-domain';
import { HttpTransportType, HubConnection, HubConnectionBuilder, IRetryPolicy, RetryContext } from '@microsoft/signalr';
import { CrisEndpoint, SignalRObservableWatcherStartOrRestartCommand } from "@local/ck-gen";

class NoRetryPolicy implements IRetryPolicy {
  nextRetryDelayInMilliseconds(retryContext: RetryContext): number | null {
    return null;
  }
}

export class SignalRObservableLeagueDomainService implements IObservableDomainLeagueDriver {
  private readonly connection: HubConnection;
  private static noRetryPolicy = new NoRetryPolicy();


  constructor(hubUrl: string, private readonly crisEndpoint: CrisEndpoint) {
    this.connection = new HubConnectionBuilder()
      .withUrl(hubUrl, HttpTransportType.None)
      .withAutomaticReconnect(SignalRObservableLeagueDomainService.noRetryPolicy)
      .build();
  }

  public async startAsync(): Promise<boolean> {
    try {
      await this.connection.start();
      return true;
    } catch (e) {
      console.log(e);
      return false;
    }
  }

  public async startListeningAsync(domainsNames: { domainName: string, transactionCount: number }[]): Promise<{ [domainName: string]: WatchEvent }> {
    const res: { [domainName: string]: WatchEvent; } = {};
    const promises = domainsNames.map(async element => {
      const poco = new SignalRObservableWatcherStartOrRestartCommand(this.connection.connectionId!, element.domainName, element.transactionCount);
      const result = await this.crisEndpoint.sendOrThrowAsync(poco);
      if (result) {
        res[element.domainName] = JSON.parse(result);
      } else {
        console.warn(`Domain ${element.domainName} doesn't exists.`);
        res[element.domainName] = "";
      }
    });
    await Promise.all(promises);
    return res;
  }

  public onMessage(eventHandler: (domainName: string, eventsJson: WatchEvent) => void) {
    this.connection.on('OnStateEventsAsync', (domainName: string, eventText: string) => {
      eventHandler(domainName, JSON.parse(eventText));
    });
  }

  public onClose(eventHandler: (error: Error | undefined) => void) {
    this.connection.onclose(eventHandler);
  }

  public async stopAsync(): Promise<void> {
    await this.connection.stop();
  }
}
