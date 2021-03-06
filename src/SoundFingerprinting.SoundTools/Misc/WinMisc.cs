﻿namespace SoundFingerprinting.SoundTools.Misc
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using System.Xml.Serialization;

    using SoundFingerprinting.Audio;
    using SoundFingerprinting.Builder;
    using SoundFingerprinting.Command;
    using SoundFingerprinting.Math;
    using SoundFingerprinting.SoundTools.Properties;
    using SoundFingerprinting.Strides;
    
    public partial class WinMisc : Form
    {
        private readonly IFingerprintCommandBuilder fingerprintCommandBuilder;

        private readonly IAudioService audioService;
        private readonly ISimilarityUtility similarityUtility;

        public WinMisc(IFingerprintCommandBuilder fingerprintCommandBuilder, IAudioService audioService, ISimilarityUtility similarityUtility)
        {
            this.fingerprintCommandBuilder = fingerprintCommandBuilder;
            this.audioService = audioService;
            this.similarityUtility = similarityUtility;

            InitializeComponent();
            Icon = Resources.Sound;
        }

        private void TbPathToFileMouseDoubleClick(object sender, MouseEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = Resources.MusicFilter, FileName = "music.mp3" };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                _tbPathToFile.Text = ofd.FileName;
            }
        }

        private void TbOutputPathMouseDoubleClick(object sender, MouseEventArgs e)
        {
            SaveFileDialog ofd = new SaveFileDialog { Filter = Resources.ExportFilter, FileName = "results.txt" };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                _tbOutputPath.Text = ofd.FileName;
            }
        }

        private void FadeControls(bool isVisible)
        {
            Invoke(new Action(
                () =>
                {
                    _tbOutputPath.Enabled = isVisible;
                    _tbPathToFile.Enabled = isVisible;
                    _nudMinFrequency.Enabled = isVisible;
                    _nudTopWavelets.Enabled = isVisible;
                    _btnDumpInfo.Enabled = isVisible;
                    _nudDatabaseStride.Enabled = isVisible;
                    _chbDatabaseStride.Enabled = isVisible;
                }));
        }

        private void ChbCompareCheckedChanged(object sender, EventArgs e)
        {
            _tbSongToCompare.Enabled = !_tbSongToCompare.Enabled;
        }

        private void TbSongToCompareMouseDoubleClick(object sender, MouseEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = Resources.MusicFilter, FileName = "music.mp3" };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                _tbSongToCompare.Text = ofd.FileName;
            }
        }

        private void BtnDumpInfoClick(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_tbPathToFile.Text))
            {
                MessageBox.Show(
                    Resources.ErrorNoFileToAnalyze,
                    Resources.SelectFile,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrEmpty(_tbOutputPath.Text))
            {
                MessageBox.Show(
                    Resources.SelectPathToDump, Resources.SelectFile, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!File.Exists(Path.GetFullPath(_tbPathToFile.Text)))
            {
                MessageBox.Show(
                    Resources.NoSuchFile, Resources.NoSuchFile, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_chbCompare.Checked)
            {
                if (string.IsNullOrEmpty(_tbSongToCompare.Text))
                {
                    MessageBox.Show(
                        Resources.ErrorNoFileToAnalyze,
                        Resources.SelectFile,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }
            }

            FadeControls(false);

            Task.Factory.StartNew(
                () =>
                    {
                        bool normalizeSignal = _cbNormalize.Checked;

                        int secondsToProcess = (int)_nudSecondsToProcess.Value;
                        int startAtSecond = (int)_nudStartAtSecond.Value;
                        int firstQueryStride = (int)_nudFirstQueryStride.Value;

                        var databaseSong = fingerprintCommandBuilder.BuildFingerprintCommand()
                                                  .From(_tbPathToFile.Text, secondsToProcess, startAtSecond)
                                                  .WithFingerprintConfig(
                                                    config =>
                                                        {
                                                            config.SpectrogramConfig.FrequencyRange.Min = (int)_nudMinFrequency.Value;
                                                            config.TopWavelets = (int)_nudTopWavelets.Value;
                                                            config.SpectrogramConfig.Stride = _chbDatabaseStride.Checked
                                                                                ? (IStride)
                                                                                  new IncrementalRandomStride(0, (int)_nudDatabaseStride.Value, config.SamplesPerFingerprint)
                                                                                : new IncrementalStaticStride((int)_nudDatabaseStride.Value, config.SamplesPerFingerprint);
                                                            config.NormalizeSignal = normalizeSignal;
                                                            config.SpectrogramConfig.UseDynamicLogBase = _cbDynamicLog.Checked;
                                                        })
                                                    .UsingServices(audioService);

                        IFingerprintCommand querySong;
                        int comparisonStride = (int)_nudQueryStride.Value;
                        if (_chbCompare.Checked)
                        {
                            querySong =
                                fingerprintCommandBuilder.BuildFingerprintCommand()
                                                          .From(_tbSongToCompare.Text, secondsToProcess, startAtSecond)
                                                          .WithFingerprintConfig(
                                                              config =>
                                                                  {
                                                                      config.SpectrogramConfig.FrequencyRange.Min = (int)_nudMinFrequency.Value;
                                                                      config.TopWavelets = (int)_nudTopWavelets.Value;
                                                                      config.SpectrogramConfig.Stride = _chbQueryStride.Checked
                                                                                          ? (IStride)
                                                                                            new IncrementalRandomStride(
                                                                                                0, comparisonStride, config.SamplesPerFingerprint, firstQueryStride)
                                                                                          : new IncrementalStaticStride(
                                                                                                comparisonStride, config.SamplesPerFingerprint, firstQueryStride);
                                                                      config.NormalizeSignal = normalizeSignal;
                                                                      config.SpectrogramConfig.UseDynamicLogBase = _cbDynamicLog.Checked;
                                                                  })
                                                          .UsingServices(audioService);
                        }
                        else
                        {
                            querySong =
                                fingerprintCommandBuilder.BuildFingerprintCommand()
                                                          .From(_tbPathToFile.Text, secondsToProcess, startAtSecond)
                                                          .WithFingerprintConfig(
                                                              config =>
                                                              {
                                                                  config.SpectrogramConfig.FrequencyRange.Min = (int)_nudMinFrequency.Value;
                                                                  config.TopWavelets = (int)_nudTopWavelets.Value;
                                                                  config.SpectrogramConfig.Stride = _chbQueryStride.Checked
                                                                                      ? (IStride)
                                                                                        new IncrementalRandomStride(
                                                                                            0, comparisonStride, config.SamplesPerFingerprint, firstQueryStride)
                                                                                      : new IncrementalStaticStride(
                                                                                            comparisonStride, config.SamplesPerFingerprint, firstQueryStride);
                                                                  config.NormalizeSignal = normalizeSignal;
                                                                  config.SpectrogramConfig.UseDynamicLogBase = _cbDynamicLog.Checked;
                                                              })
                                                         .UsingServices(audioService);
                        }

                        SimilarityResult similarityResult = new SimilarityResult();
                        string pathToInput = _tbPathToFile.Text;
                        string pathToOutput = _tbOutputPath.Text;
                        int iterations = (int)_nudIterations.Value;
                        int hashTables = (int)_nudTables.Value;
                        int hashKeys = (int)_nudKeys.Value;

                        for (int i = 0; i < iterations; i++)
                        {
                            GetFingerprintSimilarity(databaseSong, querySong, similarityResult);
                        }

                        similarityResult.Info.MinFrequency = (int)_nudMinFrequency.Value;
                        similarityResult.Info.TopWavelets = (int)_nudTopWavelets.Value;
                        similarityResult.Info.IsQueryStrideRandom = _chbQueryStride.Checked;
                        similarityResult.Info.IsDatabaseStrideRandom = _chbDatabaseStride.Checked;
                        similarityResult.Info.Filename = pathToInput;
                        similarityResult.Info.QueryStrideSize = (int)_nudQueryStride.Value;
                        similarityResult.Info.DatabaseStrideSize = (int)_nudDatabaseStride.Value;
                        similarityResult.Info.QueryFirstStrideSize = (int)_nudFirstQueryStride.Value;
                        similarityResult.Info.Iterations = iterations;
                        similarityResult.Info.HashTables = hashTables;
                        similarityResult.Info.HashKeys = hashKeys;
                        similarityResult.ComparisonDone = _chbCompare.Checked;
                        
                        if (_chbCompare.Checked)
                        {
                            similarityResult.Info.ComparedWithFile = _tbSongToCompare.Text;
                        }

                        similarityResult.SumJaqSimilarityBetweenDatabaseAndQuerySong /= iterations;
                        similarityResult.AverageJaqSimilarityBetweenDatabaseAndQuerySong /= iterations;
                        similarityResult.AtLeastOneTableWillVoteForTheCandidate = 1 - Math.Pow(1 - Math.Pow(similarityResult.AverageJaqSimilarityBetweenDatabaseAndQuerySong, hashKeys), hashTables);
                        similarityResult.AtLeastOneHashbucketFromHashtableWillBeConsideredACandidate = Math.Pow(similarityResult.AverageJaqSimilarityBetweenDatabaseAndQuerySong, hashKeys);
                        similarityResult.WillBecomeACandidateByPassingThreshold = Math.Pow(similarityResult.AtLeastOneHashbucketFromHashtableWillBeConsideredACandidate, (int)_nudCandidateThreshold.Value);

                        using (TextWriter writer = new StreamWriter(pathToOutput))
                        {
                            XmlSerializer serializer = new XmlSerializer(typeof(SimilarityResult));
                            serializer.Serialize(writer, similarityResult);
                            writer.Close();
                        }
                    }).ContinueWith(result => FadeControls(true));
        }

        private void GetFingerprintSimilarity(IFingerprintCommand databaseSong, IFingerprintCommand querySong, SimilarityResult results)
        {
            double sum = 0;

            var fingerprintsDatabaseSong = databaseSong.Fingerprint()
                                                                .Result
                                                                .Select(fingerprint => fingerprint)
                                                                .ToList();
            var fingerprintsQuerySong = querySong.Fingerprint()
                                                                .Result
                                                                .Select(fingerprint => fingerprint)
                                                                .ToList();
            
            double max = double.MinValue;
            double min = double.MaxValue;
            int comparisonsCount = 0;
            for (int i = 0; i < fingerprintsDatabaseSong.Count; i++)
            {
                for (int j = 0; j < fingerprintsQuerySong.Count; j++)
                {
                    double value = similarityUtility.CalculateJaccardSimilarity(fingerprintsDatabaseSong[i].Signature, fingerprintsQuerySong[j].Signature);
                    if (value > max)
                    {
                        max = value;
                    }

                    if (value < min)
                    {
                        min = value;
                    }

                    sum += value;
                    comparisonsCount++;
                }
            }

            results.SumJaqSimilarityBetweenDatabaseAndQuerySong += sum;
            results.AverageJaqSimilarityBetweenDatabaseAndQuerySong += sum / comparisonsCount;
            if (max > results.MaxJaqSimilarityBetweenDatabaseAndQuerySong)
            {
                results.MaxJaqSimilarityBetweenDatabaseAndQuerySong = max;
            }

            if (min < results.MinJaqSimilarityBetweenDatabaseAndQuerySong)
            {
                results.MinJaqSimilarityBetweenDatabaseAndQuerySong = min;
            }

            results.NumberOfAnalizedFingerprints = comparisonsCount;
        }
    }
}