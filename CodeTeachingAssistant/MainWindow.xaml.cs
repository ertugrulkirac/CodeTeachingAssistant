using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;

namespace CodeTeachingAssistant
{
    public partial class MainWindow : Window
    {
        private int quizScore = 0;
        private string correctAnswer = "";
        private readonly string tempCodePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "user_code.py");
        private readonly string pythonScriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "analyze.py");
        private readonly string historyFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "history.json");
        private readonly string tempSyntaxCodePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "user_code_temp.py");
        private readonly string syntaxCheckerPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "syntax_checker.py");
        private readonly string pythonExePath = @"C:\\Users\\ekirac\\AppData\\Local\\Programs\\Python\\Python312\\python.exe";

        private List<AnalysisRecord> analysisHistory = new();
        private List<string> history = new();
        private Dictionary<string, int> errorCounts = new();
        private int totalAnalysis = 0;
        private int syntaxCleanCount = 0;
        private int currentQuestionIndex = 0;
        private bool isQuestionAnswered = false;
        private List<QuizQuestion> remainingQuestions = new();
        private Dictionary<string, string> keywordMap = new();

        private class QuizQuestion
        {
            [JsonPropertyName("question")]
            public string Question { get; set; }

            [JsonPropertyName("options")]
            public string[] Options { get; set; }

            [JsonPropertyName("answer")]
            public string Answer { get; set; }
        }

        private List<QuizQuestion> quizQuestions = new();

        public MainWindow()
        {
            InitializeComponent();
            LoadHistoryFromFile();
            UpdateHistoryListBox();
            LoadQuizFromJson();
            LoadNewQuestion_Click(null, null);
        }

        private void LoadQuizFromJson()
        {
            try
            {
                string quizPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "quiz_questions.json");
                if (File.Exists(quizPath))
                {
                    string json = File.ReadAllText(quizPath);
                    quizQuestions = JsonSerializer.Deserialize<List<QuizQuestion>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                    remainingQuestions = quizQuestions.OrderBy(x => Guid.NewGuid()).ToList();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Quiz verisi yüklenemedi: " + ex.Message);
                quizQuestions = new();
                remainingQuestions = new();
            }
        }

        private void LoadNewQuestion_Click(object sender, RoutedEventArgs e)
        {
            if (!isQuestionAnswered && currentQuestionIndex > 0)
            {
                QuizWarningText.Text = "Lütfen mevcut soruyu cevaplayın.";
                return;
            }

            if (remainingQuestions.Count == 0)
            {
                QuizQuestionText.Text = "🎉 Quiz bitti. Toplam skor: " + quizScore;
                OptionA.Visibility = OptionB.Visibility = OptionC.Visibility = OptionD.Visibility = Visibility.Hidden;
                NextQuestionButton.IsEnabled = false;
                return;
            }

            var currentQuestion = remainingQuestions.First();
            remainingQuestions.RemoveAt(0);
            currentQuestionIndex++;

            QuizQuestionText.Text = $"Soru {currentQuestionIndex}: {currentQuestion.Question}";

            var rand = new Random();
            var shuffled = currentQuestion.Options.OrderBy(x => rand.Next()).ToArray();

            OptionA.Content = shuffled.ElementAtOrDefault(0);
            OptionB.Content = shuffled.ElementAtOrDefault(1);
            OptionC.Content = shuffled.ElementAtOrDefault(2);
            OptionD.Content = shuffled.ElementAtOrDefault(3);

            OptionA.IsChecked = false;
            OptionB.IsChecked = false;
            OptionC.IsChecked = false;
            OptionD.IsChecked = false;

            correctAnswer = currentQuestion.Answer;
            isQuestionAnswered = false;
            QuizWarningText.Text = "";
            NextQuestionButton.IsEnabled = false;
        }

        private void SubmitAnswer_Click(object sender, RoutedEventArgs e)
        {
            string selected = null;
            if (OptionA.IsChecked == true) selected = OptionA.Content.ToString();
            else if (OptionB.IsChecked == true) selected = OptionB.Content.ToString();
            else if (OptionC.IsChecked == true) selected = OptionC.Content.ToString();
            else if (OptionD.IsChecked == true) selected = OptionD.Content.ToString();

            if (selected == null)
            {
                QuizWarningText.Text = "Lütfen bir seçenek seçin.";
                return;
            }

            if (selected == correctAnswer)
            {
                quizScore++;
                QuizWarningText.Text = "✅ Doğru cevap!";
            }
            else
            {
                QuizWarningText.Text = $"❌ Yanlış. Doğru cevap: {correctAnswer}";
            }

            QuizScoreText.Text = $"Skor: {quizScore}";

            string badge = "";
            if (quizScore >= 10) badge = "🏆 Altın Rozet!";
            else if (quizScore >= 5) badge = "🥈 Gümüş Rozet!";
            else if (quizScore >= 3) badge = "🥉 Bronz Rozet!";

            if (!string.IsNullOrEmpty(badge))
                QuizWarningText.Text += $"  {badge}";

            NextQuestionButton.IsEnabled = true;
            isQuestionAnswered = true;
        }

        private void AnalyzeCode_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CodeEditor.Text))
            {
                FeedbackBox.Text = "Lütfen analiz edilecek bir kod girin.";
                return;
            }

            try
            {
                File.WriteAllText(tempCodePath, CodeEditor.Text);

                var psi = new ProcessStartInfo
                {
                    FileName = pythonExePath,
                    Arguments = $"\"{pythonScriptPath}\" \"{tempCodePath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                FeedbackBox.Text = "[Analiz Çıktısı]\n" + output + "\n\n[Hata Çıkışı]\n" + error;

                history.Add(output);
                if (history.Count > 5)
                    history.RemoveAt(0);
                UpdateHistoryListBox();

                totalAnalysis++;
                if (!output.Contains("Sözdizimi Hatası"))
                    syntaxCleanCount++;

                string mostFrequentError = ExtractMostFrequentError(output);
                UpdateStatistics(totalAnalysis, syntaxCleanCount, mostFrequentError);
                UpdateKeywordInfoCards(output);

                var record = new AnalysisRecord
                {
                    Time = DateTime.Now.ToShortTimeString(),
                    Output = output,
                    IsSyntaxClean = !output.Contains("Sözdizimi Hatası"),
                    Errors = ExtractErrors(output)
                };

                analysisHistory.Add(record);
                if (analysisHistory.Count > 50)
                    analysisHistory.RemoveAt(0);

                File.WriteAllText(historyFile, JsonSerializer.Serialize(analysisHistory));
            }
            catch (Exception ex)
            {
                FeedbackBox.Text = "Hata oluştu:\n" + ex.Message;
            }
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!File.Exists(historyFile))
                {
                    MessageBox.Show("Geçmiş bulunamadı.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string json = File.ReadAllText(historyFile);
                var history = JsonSerializer.Deserialize<List<AnalysisRecord>>(json);

                if (history == null || history.Count == 0)
                {
                    MessageBox.Show("Hiç analiz yapılmamış.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                StringBuilder csv = new();
                csv.AppendLine("Zaman,Hatalar,SyntaxTemiz,ÇıktıÖzeti");

                foreach (var record in history)
                {
                    string hatalar = string.Join(" | ", record.Errors);
                    string ozetsatir = record.Output.Replace("\n", " ").Replace("\r", " ");
                    if (ozetsatir.Length > 100)
                        ozetsatir = ozetsatir.Substring(0, 100) + "...";

                    csv.AppendLine($"{record.Time},\"{hatalar}\",{record.IsSyntaxClean},\"{ozetsatir}\"");
                }

                string csvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "analysis_report.csv");
                File.WriteAllText(csvPath, csv.ToString(), Encoding.UTF8);

                MessageBox.Show("CSV başarıyla dışa aktarıldı:\n" + csvPath, "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("CSV dışa aktarım hatası:\n" + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<string> ExtractErrors(string output)
        {
            return output.Split('\n')
                         .Where(line => line.Contains("Hata") || line.Contains("PEP8"))
                         .Select(line => line.Trim())
                         .ToList();
        }

        private string ExtractMostFrequentError(string output)
        {
            var lines = output.Split('\n')
                              .Where(l => l.Contains("Hata") || l.Contains("PEP8"))
                              .Select(l => l.Trim());

            foreach (var line in lines)
            {
                if (!errorCounts.ContainsKey(line))
                    errorCounts[line] = 0;
                errorCounts[line]++;
            }

            return errorCounts.OrderByDescending(kv => kv.Value).FirstOrDefault().Key ?? "-";
        }

        private void LoadKeywordExplanations()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "keyword_explanations.json");
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    keywordMap = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Anahtar kelime açıklamaları yüklenemedi:\n" + ex.Message);
                keywordMap = new(); // Boş bırak
            }
        }

        private void UpdateKeywordInfoCards(string output)
        {
            KeywordInfoPanel.Children.Clear();

            // keywordMap'i harici dosyadan dinamik olarak alıyoruz
            foreach (var pair in keywordMap)
            {
                // Kodda anahtar kelimeyi içeriyorsa, açıklamasını göster
                if (CodeEditor.Text.Contains(pair.Key))
                {
                    KeywordInfoPanel.Children.Add(new TextBlock
                    {
                        Text = pair.Value,
                        FontSize = 14,
                        Margin = new Thickness(5)
                    });
                }
            }

            // Eğer hiç anahtar kelime bulunmadıysa, kullanıcıyı bilgilendir
            if (KeywordInfoPanel.Children.Count == 0)
            {
                KeywordInfoPanel.Children.Add(new TextBlock
                {
                    Text = "Kodda analiz edilecek bilinen anahtar kelime bulunamadı.",
                    FontSize = 14,
                    Margin = new Thickness(5)
                });
            }
        }


        private void UpdateStatistics(int total, int clean, string topError)
        {
            TotalAnalysisText.Text = total.ToString();
            CleanCountText.Text = clean.ToString();
            TopErrorText.Text = topError;
            LearningProgressBar.Value = total == 0 ? 0 : (double)clean / total * 100;
            UserLevelText.Text = DetermineUserLevel(total);
        }

        private string DetermineUserLevel(int total)
        {
            if (total >= 50)
                return "Usta";
            else if (total >= 25)
                return "İleri Seviye";
            else if (total >= 10)
                return "Orta Seviye";
            else
                return "Başlangıç";
        }

        private void LoadHistoryFromFile()
        {
            if (File.Exists(historyFile))
            {
                string json = File.ReadAllText(historyFile);
                analysisHistory = JsonSerializer.Deserialize<List<AnalysisRecord>>(json) ?? new();
                totalAnalysis = analysisHistory.Count;
                syntaxCleanCount = analysisHistory.Count(r => r.IsSyntaxClean);

                var allErrors = analysisHistory.SelectMany(r => r.Errors);
                foreach (var err in allErrors)
                {
                    if (!errorCounts.ContainsKey(err))
                        errorCounts[err] = 0;
                    errorCounts[err]++;
                }
            }
        }

        private void UpdateHistoryListBox()
        {
            HistoryListBox.Items.Clear();
            foreach (var record in analysisHistory.TakeLast(5))
            {
                HistoryListBox.Items.Add($"Analiz ({record.Time})\n{Shorten(record.Output)}");
            }
        }

        private void CodeEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                string code = CodeEditor.Text;
                if (string.IsNullOrWhiteSpace(code))
                {
                    FeedbackBox.Text = "";
                    return;
                }

                File.WriteAllText(tempSyntaxCodePath, code);

                var psi = new ProcessStartInfo
                {
                    FileName = pythonExePath,
                    Arguments = $"\"{syntaxCheckerPath}\" \"{tempSyntaxCodePath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                FeedbackBox.Text = !string.IsNullOrWhiteSpace(output) ? output : $"Hata oluştu:\n{error}";
            }
            catch (Exception ex)
            {
                FeedbackBox.Text = "Canlı analiz hatası:\n" + ex.Message;
            }
        }

        private string Shorten(string input)
        {
            return input.Length > 250 ? input[..250] + "..." : input;
        }
    }
}
