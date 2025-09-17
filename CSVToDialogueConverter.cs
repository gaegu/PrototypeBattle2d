using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using AC;
using UnityEditor;
using UnityEngine;

namespace IronJade.ResourcesAddressable._2DRenewal.PortraitNew
{
	public class CSVToDialogueConverter : EditorWindow
	{
		private string csvFilePath = "";
		private string outputPath = "Assets/Cosmos/PortraitNew/SO";
		private string outputFileName = "DialogueData";

		[MenuItem("*COSMOS*/CutScene/CSV to Dialogue Converter")]
		public static void ShowWindow()
		{
			GetWindow<CSVToDialogueConverter>("CSV to Dialogue Converter");
		}

		private void OnGUI()
		{
			GUILayout.Label("CSV to Dialogue ScriptableObject Converter", EditorStyles.boldLabel);
			
			GUILayout.Space(10);
			
			GUILayout.Label("CSV 파일 선택:");
			EditorGUILayout.BeginHorizontal();
			csvFilePath = EditorGUILayout.TextField(csvFilePath);
			if (GUILayout.Button("Browse", GUILayout.Width(60)))
			{
				string path = EditorUtility.OpenFilePanel("CSV 파일 선택", "", "csv");
				if (!string.IsNullOrEmpty(path))
				{
					csvFilePath = path;
				}
			}
			EditorGUILayout.EndHorizontal();
			
			GUILayout.Space(10);
			
			GUILayout.Label("출력 설정:");
			outputPath = EditorGUILayout.TextField("출력 경로:", outputPath);
			outputFileName = EditorGUILayout.TextField("파일명:", outputFileName);
			
			GUILayout.Space(20);
			
			if (GUILayout.Button("CSV를 ScriptableObject로 변환"))
			{
				ConvertCSVToScriptableObject();
			}
			
			GUILayout.Space(10);
			
			if (GUILayout.Button("폴더 열기"))
			{
				if (Directory.Exists(outputPath))
				{
					EditorUtility.RevealInFinder(outputPath);
				}
			}
		}

		private void ConvertCSVToScriptableObject()
		{
			if (string.IsNullOrEmpty(csvFilePath) || !File.Exists(csvFilePath))
			{
				EditorUtility.DisplayDialog("오류", "유효한 CSV 파일을 선택해주세요.", "확인");
				return;
			}

			try
			{
				// CSV 파일 읽기
				string[] lines = File.ReadAllLines(csvFilePath);
				if (lines.Length < 2)
				{
					EditorUtility.DisplayDialog("오류", "CSV 파일이 비어있거나 헤더만 있습니다.", "확인");
					return;
				}

				// 헤더 제거하고 데이터 파싱
				List<DialogueEntry> entries = new List<DialogueEntry>();
				
				for (int i = 1; i < lines.Length; i++) // 첫 번째 줄(헤더) 제외
				{
					string line = lines[i];
					if (string.IsNullOrEmpty(line)) continue;

					DialogueEntry entry = ParseCSVLine(line);
					if (entry != null)
					{
						entries.Add(entry);
					}
				}

				// ScriptableObject 생성
				DialogueData dialogueData = ScriptableObject.CreateInstance<DialogueData>();
				dialogueData.entries = entries.ToArray();

				// 출력 디렉토리 생성
				if (!Directory.Exists(outputPath))
				{
					Directory.CreateDirectory(outputPath);
				}

				// 파일 저장 (기존 있으면 삭제 후 생성)
				string fullPath = Path.Combine(outputPath, outputFileName + ".asset");
				var existing = AssetDatabase.LoadAssetAtPath<DialogueData>(fullPath);
				if (existing != null)
				{
					AssetDatabase.DeleteAsset(fullPath);
				}
				AssetDatabase.CreateAsset(dialogueData, fullPath);
				AssetDatabase.SaveAssets();
				AssetDatabase.Refresh();

				EditorUtility.DisplayDialog("성공", $"변환이 완료되었습니다!\n저장 위치: {fullPath}", "확인");
				
				// 생성된 파일 선택
				Selection.activeObject = dialogueData;
				EditorGUIUtility.PingObject(dialogueData);
			}
			catch (System.Exception e)
			{
				EditorUtility.DisplayDialog("오류", $"변환 중 오류가 발생했습니다:\n{e.Message}", "확인");
				Debug.LogError($"CSV 변환 오류: {e}");
			}
		}

		private DialogueEntry ParseCSVLine(string line)
		{
			string[] fields = ParseCSVFields(line);
			
			// 후처리: 모든 필드 정규화(트림, 양끝 따옴표 제거, 이스케이프 정리)
			for (int fi = 0; fi < fields.Length; fi++)
			{
				fields[fi] = CleanCsvField(fields[fi]);
			}
			
			if (fields.Length < 3)
			{
				Debug.LogError($"CSV 라인 필드 수가 부족합니다: {line}");
				return null;
			}

			DialogueEntry entry = new DialogueEntry();
			
			// ID 파싱
			if (int.TryParse(fields[0], out int id))
			{
				entry.id = id;
			}
			else
			{
				Debug.LogError($"ID 파싱 실패: {fields[0]}");
				return null;
			}

			// DataType 파싱
			if (System.Enum.TryParse<DialogueDataType>(fields[1], true, out DialogueDataType dataType))
			{
				entry.dataType = dataType;
			}
			else
			{
				Debug.LogError($"DataType 파싱 실패: {fields[1]}");
				return null;
			}

			// DataType에 따른 필드 파싱
			switch (entry.dataType)
			{
				case DialogueDataType.Dialogue:
					if (fields.Length >= 4)
					{
						entry.character = fields[2];
						entry.dialogue = fields[3];
					}
					break;
					
				case DialogueDataType.ImageToon:
					if (fields.Length >= 3)
					{
						entry.imagePath = fields[2];
					}
					break;
					
				case DialogueDataType.Sound:
					if (fields.Length >= 3)
					{
						entry.soundPath = fields[2];
					}
					break;
					
				case DialogueDataType.Action:
					if (fields.Length >= 3)
					{
						if (System.Enum.TryParse<ActionType>(fields[2], true, out ActionType actionType))
						{
							entry.actionType = actionType;
						}
						
						// 추가 액션 데이터 파싱
						if (fields.Length >= 4)
						{
							switch (entry.actionType)
							{
								case ActionType.Move:
									// Transform 참조는 나중에 설정
									break;
								case ActionType.Animation:
									entry.animationName = fields[3];
									break;
								case ActionType.Emotion:
									entry.emotionType = fields[3];
									break;
							}
						}
					}
					break;
					
				                case DialogueDataType.DialogueToon:
                    if (fields.Length >= 4)
                    {
                        entry.character = fields[2];
                        entry.dialogue = fields[3];
                        if (fields.Length >= 5)
                        {
                            entry.imagePath = fields[4];
                        }
                    }
                    break;
                    
                case DialogueDataType.Custom:
                    if (fields.Length >= 4)
                    {
                        entry.customJson = fields[2]; // JSON 데이터
                        if (fields.Length >= 5)
                        {
                            // Character 컬럼에 커스텀 타입을 저장 (예: "CameraShake", "ParticleEffect" 등)
                            entry.character = fields[3];
                        }
                    }
                    break;
			}

			return entry;
		}

		private string[] ParseCSVFields(string line)
		{
			List<string> fields = new List<string>();
			bool inQuotes = false;
			string currentField = "";
			
			for (int i = 0; i < line.Length; i++)
			{
				char c = line[i];
				
				if (c == '"')
				{
					if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
					{
						// 이스케이프된 따옴표
						currentField += '"';
						i++; // 다음 따옴표 건너뛰기
					}
					else
					{
						// 따옴표 시작/끝 - 따옴표는 추가하지 않음
						inQuotes = !inQuotes;
					}
				}
				else if (c == ',' && !inQuotes)
				{
					// 필드 구분자
					fields.Add(currentField.Trim()); // Trim으로 앞뒤 공백도 제거
					currentField = "";
				}
				else
				{
					currentField += c;
				}
			}
			
			// 마지막 필드 추가
			fields.Add(currentField.Trim()); // Trim으로 앞뒤 공백도 제거
			
			return fields.ToArray();
		}

		private string CleanCsvField(string value)
		{
			if (string.IsNullOrEmpty(value)) return value;
			string v = value.Trim();
			// 연속 이스케이프 따옴표 축약
			v = v.Replace("\"\"", "\"");
			// 제거 대상 따옴표 집합
			char[] quoteChars = new char[] { '"', '\u201C', '\u201D', '\u201E', '\u00AB', '\u00BB', '\uFF02' };
			// 앞뒤 따옴표를 반복적으로 제거 (혼합된 경우도 처리)
			bool stripped = true;
			while (v.Length >= 2 && stripped)
			{
				stripped = false;
				char first = v[0];
				char last = v[v.Length - 1];
				bool firstIsQuote = System.Array.IndexOf(quoteChars, first) >= 0;
				bool lastIsQuote = System.Array.IndexOf(quoteChars, last) >= 0;
				if (firstIsQuote && lastIsQuote)
				{
					v = v.Substring(1, v.Length - 2).Trim();
					stripped = true;
				}
			}
			return v;
		}
	}
}
