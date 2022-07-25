import { Observable } from 'rxjs';
import { WatchEvent } from '@signature-code/ck-observable-domain';


export interface IObservableDomainLeagueDriver {
    start() : Promise<void>;
    onMessage(eventHandler: (domainName: string, eventsJson: WatchEvent) => void): void;
    onClose(eventHandler: (error: Error) => void): void;
    stop(): Promise<void>;
}
