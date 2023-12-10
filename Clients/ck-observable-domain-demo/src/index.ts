import axios from "axios";
import {MqttObservableLeagueDomainService} from "@signature/ck-observable-domain-mqtt"
import {ObservableDomainClient} from "@signature/ck-observable-domain";
import {HttpCrisEndpoint, SliderCommand} from "@local/ck-gen";
import {SignalRObservableLeagueDomainService} from "@signature/ck-observable-domain-signalr";

const crisAxios = axios.create();
const crisEndpoint = new HttpCrisEndpoint(crisAxios, "http://localhost:5000/.cris");

(async function (): Promise<void> {
    const mqttOdDriver = new MqttObservableLeagueDomainService("ws://localhost:8080/mqtt/", crisEndpoint, "CK-OD");
    const signalROdDriver = new SignalRObservableLeagueDomainService("http://localhost:5000/hub/league", crisEndpoint);

    const mqttOdClient = new ObservableDomainClient(mqttOdDriver);
    mqttOdClient.start();
    const signalrOdClient = new ObservableDomainClient(signalROdDriver);
    signalrOdClient.start();
    const mqttOdObs = await mqttOdClient.listenToDomainAsync("Test-Domain");
    const signalROdObs = await signalrOdClient.listenToDomainAsync("Test-Domain");
    const left = document.getElementById("slider-left") as HTMLInputElement;
    const right = document.getElementById("slider-right") as HTMLInputElement;

    mqttOdObs.forEach((s) => {
        if (s.length < 1) return;
        console.log("Mqtt value: " + s[0].Slider);
        left.value = s[0].Slider
    });

    signalROdObs.forEach((s) => {
        if (s.length < 1) return;
        console.log("SignalR value: " + s[0].Slider);
        right.value = s[0].Slider;
    });
})();

export async function sliderUpdate(sliderValue: unknown): Promise<void> {
    await crisEndpoint.sendOrThrowAsync<void>(SliderCommand.create(Number(sliderValue)));
}

window["sliderUpdate"] = sliderUpdate;
