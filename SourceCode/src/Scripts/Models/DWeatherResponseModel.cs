using System;
using Newtonsoft.Json;

namespace NyuBot.Weather {
	public class DWeatherResponseModel {

		[JsonProperty("cacheTime")]
		public string CacheTime;

		[JsonProperty("cod")]
		public int ErrorCode;

		[JsonProperty("name")]
		public string LocalName;
		
		[JsonProperty("main")] 
		public WeatherTemperatureModel MainWeatherTemperature;
		
		[JsonProperty("weather")] 
		public WeatherInfoModel[] WeatherInfoModel;

		[JsonProperty("wind")] 
		public WeatherWindModel Wind;

	}

	public struct WeatherTemperatureModel {
		[JsonProperty("temp")] 
		public float TemperatureKelvin;
		[JsonIgnore]
		public float TemperatureCelsius {
			get {
				return this.TemperatureKelvin - 273.15f;
			}
		}
		
		[JsonProperty("humidity")] 
		public string Humidity;
		
		[JsonProperty("feels_like")] 
		public float FeelsLikeKevin;
		
		[JsonIgnore]
		public float FeelsLikeCelsius {
			get {
				return this.FeelsLikeKevin - 273.15f;
			}
		}
	}

	public struct WeatherInfoModel {
		[JsonProperty("icon")]
		public string Icon;
		
		[JsonProperty("main")]
		public string Main;
		
		[JsonProperty("description")]
		public string Description;
	}

	public struct WeatherWindModel {
		[JsonProperty("speed")]
		public float Speed;
	}
}
