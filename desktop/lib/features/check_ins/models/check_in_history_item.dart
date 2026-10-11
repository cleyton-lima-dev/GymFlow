enum CheckInSource {
  manual(1),
  physicalAccess(2),
  wellhub(3),
  otherIntegration(99);

  const CheckInSource(this.value);

  final int value;

  static CheckInSource fromValue(int value) {
    return CheckInSource.values.firstWhere(
      (source) => source.value == value,
      orElse: () => CheckInSource.otherIntegration,
    );
  }
}

class CheckInHistoryItem {
  const CheckInHistoryItem({
    required this.id,
    required this.studentId,
    required this.studentName,
    required this.source,
    required this.physicalAccessEventId,
    required this.recordedByUserId,
    required this.occurredAt,
  });

  final String id;
  final String studentId;
  final String studentName;
  final CheckInSource source;
  final String? physicalAccessEventId;
  final String? recordedByUserId;
  final DateTime occurredAt;

  factory CheckInHistoryItem.fromJson(Map<String, dynamic> json) {
    return CheckInHistoryItem(
      id: json['id'] as String,
      studentId: json['studentId'] as String,
      studentName: json['studentName'] as String,
      source: CheckInSource.fromValue((json['source'] as num).toInt()),
      physicalAccessEventId: json['physicalAccessEventId'] as String?,
      recordedByUserId: json['recordedByUserId'] as String?,
      occurredAt: DateTime.parse(json['occurredAt'] as String),
    );
  }
}
