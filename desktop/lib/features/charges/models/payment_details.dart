enum PaymentMethod {
  pix(1, 'Pix'),
  card(2, 'Cartão'),
  cash(3, 'Dinheiro'),
  other(4, 'Outro');

  const PaymentMethod(
      this.value,
      this.label,
      );

  final int value;
  final String label;

  static PaymentMethod? fromValue(int? value) {
    if (value == null) {
      return null;
    }

    for (final method in values) {
      if (method.value == value) {
        return method;
      }
    }

    return null;
  }
}

class PaymentDetails {
  const PaymentDetails({
    required this.paidAmount,
    required this.discountAmount,
    required this.paymentMethod,
    required this.paidAt,
  });

  final double paidAmount;
  final double discountAmount;
  final PaymentMethod paymentMethod;
  final DateTime paidAt;

  Map<String, dynamic> toJson() {
    return {
      'paidAmount': paidAmount,
      'discountAmount': discountAmount,
      'paymentMethod': paymentMethod.value,
      'paidAt': paidAt.toUtc().toIso8601String(),
    };
  }
}
