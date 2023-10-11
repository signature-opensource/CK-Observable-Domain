/**
* To manage ambient values overrides, we use the null value to NOT override:
*  - We decided to map C# null to undefined because working with both null
*    and undefined is difficult.
*  - Here, the null is used, so that undefined can be used to override with an undefined that will
*    be a null value on the C# side.
* All the properties are initialized to null in the constructor.
**/
export class AmbientValuesOverride {
actorId: number|null;
actualActorId: number|null;
deviceId: string|null;
constructor() {
this.actorId = null;
this.actualActorId = null;
this.deviceId = null;

}

}
