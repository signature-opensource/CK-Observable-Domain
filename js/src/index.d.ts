export interface ObservableDomainEvent {
    N: number;
    E: any[];
}
export interface ObservableDomainState {
    N: number;
    P: string[];
    O: any;
    R: number[];
}
export declare class ObservableDomain {
    private readonly _props;
    private _tranNum;
    private _objCount;
    private readonly _graph;
    private readonly _roots;
    constructor(initialState: string | ObservableDomainState);
    readonly transactionNumber: number;
    readonly allObjectsCount: number;
    readonly allObjects: Iterable<any>;
    readonly roots: ReadonlyArray<any>;
    applyEvent(event: ObservableDomainEvent): void;
    private getValue;
}
//# sourceMappingURL=index.d.ts.map