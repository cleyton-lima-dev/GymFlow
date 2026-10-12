import 'dart:typed_data';

import 'package:avelri_gestao/core/network/api_client.dart';

class ReportResult {
  ReportResult.fromJson(Map<String, dynamic> json)
    : title = json['title'] as String,
      basis = json['periodBasis'] as String,
      timeZone = json['timeZone'] as String,
      fromDate = json['fromDate'] as String?,
      toDate = json['toDate'] as String?,
      columns = List<String>.from(json['columns'] as List),
      rows = (json['rows'] as List)
          .map((r) => List<String>.from(r as List))
          .toList(),
      total = json['totalCount'] as int,
      page = json['page'] as int,
      pageSize = json['pageSize'] as int,
      totals = Map<String, String>.from(json['totals'] as Map);
  final String title, basis, timeZone;
  final String? fromDate, toDate;
  final List<String> columns;
  final List<List<String>> rows;
  final int total, page, pageSize;
  final Map<String, String> totals;
  int get pages => (total / pageSize).ceil();
}

class ReportsService {
  ReportsService(this.api);
  final ApiClient api;
  String path(
    String kind,
    Map<String, String> filters, {
    bool export = false,
  }) => Uri(
    path: '/api/reports/$kind${export ? '/export' : ''}',
    queryParameters: filters,
  ).toString();
  Future<ReportResult> load(String kind, Map<String, String> filters) async =>
      ReportResult.fromJson(
        await api.get(path(kind, filters)) as Map<String, dynamic>,
      );
  Future<Uint8List> export(String kind, Map<String, String> filters) =>
      api.getBytes(path(kind, filters, export: true));
}
