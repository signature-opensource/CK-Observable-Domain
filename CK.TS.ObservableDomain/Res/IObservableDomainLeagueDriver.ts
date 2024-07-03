import { WatchEvent } from './ObservableDomain';

export interface IObservableDomainLeagueDriver {
    startAsync(): Promise<boolean>;
    startListeningAsync(domainsNames: { domainName: string, transactionCount: number }[]): Promise<{ [domainName: string]: WatchEvent }>;
    onMessage(eventHandler: (domainName: string, eventsJson: WatchEvent) => void): void;
    onClose(eventHandler: (error: Error | undefined) => void): void;
    stopAsync(): Promise<void>;
}
