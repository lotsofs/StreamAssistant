namespace StreamAssistant2 {
	public static class Util {
		public static float TrueModulo(float a, float m) {
			return (a % m + m) % m;
		}
		public static double TrueModulo(double a, double m) {
			return (a % m + m) % m;
		}
		public static int TrueModulo(int a, int m) {
			return (a % m + m) % m;
		}
		public static long TrueModulo(long a, long m) {
			return (a % m + m) % m;
		}
	}
}
