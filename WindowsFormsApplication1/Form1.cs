using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using System.IO.Ports;
using System.Globalization;
using System.Net;
namespace WindowsFormsApplication1
{
    public partial class Form1 : Form
    {   
        
        public enum dataExchangeTemplates: byte{singleMessageTemplate = 0, deviceSearchTemplate = 1, flashLoadTemplate = 2};
        byte[] templateArr; //массив, задающий шаблон обмена сообщениями, в нем каждый элемент равен номеру отправляемой команды (команда AA = запись 4 байт в память МК, команда AB = завершение записи), любое другое число соответствует команде HART
        int templateArrIndex; //указывает на текущий индекс массива шаблонов (команду, отправляемую на данном шаге)
        public byte[] dataToWriteInPort;
        int CRCErrorsCounter = 0;
        int tableCellDataIndex = 0;
        string spRead;
        private BackgroundWorker backgroundWorker1;
        int ReadBytesLastCycle = 0;
        int ReadDataCRCError;//счетчик ошибок, считает до 5ти, а дальше отключает опрос
        bool ReadDataCRCOk = false;
        int CRCerrors = 0;
        int MessagesRecieved = 0;
        delegate void SetTextCallback(string text);
        private Thread demoThread = null;
        byte[] recieveBuff = new byte[128];
        int HowManyBytes = 0;
        int recieveBuffIndex = 0;
        byte firstSearchAddress = 0;
        byte lastSearchAddress = 15;
        bool answerTimedOut = false;
        byte templateSelected = 0;
        int progressBarCounter = 0;

        
        public struct devices
        {
            public byte address;
            public byte devType;
            public string devName;
            public enum firmware: int { bootloader=0, application=1, undefined=2 };
            public int serialNum;
            public byte firmwareRevision;
            public bool enabled;
            public string[] devs;// = new string[5];
            public void setValues(int val)
            {
                devs = new string[5];
                    devs[0] = address.ToString();
                    devs[1] = devName;
                    switch (val)
                    {
                        case 0:
                            {
                                devs[2] = firmware.bootloader.ToString();
                                break;
                            }
                        case 1:
                            {
                                devs[2] = firmware.application.ToString();
                                break;
                            }
                        default:
                            {
                                devs[2] = firmware.undefined.ToString();
                                break;
                            }
                    }
                    devs[3] = serialNum.ToString();
                    devs[4] = firmwareRevision.ToString();


            }
            

        }

        public devices[] devicesArr = new devices[15];
        public Form1()
        {
            InitializeComponent();
           
           // this.Width = 749;
           // this.Height = 616;
 //           this.backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
 //           this.backgroundWorker1.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.backgroundWorker1_RunWorkerCompleted);


        }

        private bool checkCRC16()
        {
             ushort crc = 0x0000;
             byte crcPrgLow = 0;
             byte crcPrgHigh = 0;
             ushort[] crctable= new ushort[256] { 0x0000, 0xC1C0, 0x81C1, 0x4001, 0x01C3, 0xC003, 0x8002, 0x41C2, 0x01C6, 0xC006,
                                            0x8007, 0x41C7, 0x0005, 0xC1C5, 0x81C4, 0x4004, 0x01CC, 0xC00C, 0x800D, 0x41CD,
                                            0x000F, 0xC1CF, 0x81CE, 0x400E, 0x000A, 0xC1CA, 0x81CB, 0x400B, 0x01C9, 0xC009,
                                            0x8008, 0x41C8, 0x01D8, 0xC018, 0x8019, 0x41D9, 0x001B, 0xC1DB, 0x81DA, 0x401A,
                                            0x001E, 0xC1DE, 0x81DF, 0x401F, 0x01DD, 0xC01D, 0x801C, 0x41DC, 0x0014, 0xC1D4,
                                            0x81D5, 0x4015, 0x01D7, 0xC017, 0x8016, 0x41D6, 0x01D2, 0xC012, 0x8013, 0x41D3,
                                            0x0011, 0xC1D1, 0x81D0, 0x4010, 0x01F0, 0xC030, 0x8031, 0x41F1, 0x0033, 0xC1F3,
                                            0x81F2, 0x4032, 0x0036, 0xC1F6, 0x81F7, 0x4037, 0x01F5, 0xC035, 0x8034, 0x41F4,
                                            0x003C, 0xC1FC, 0x81FD, 0x403D, 0x01FF, 0xC03F, 0x803E, 0x41FE, 0x01FA, 0xC03A, 
                                            0x803B, 0x41FB, 0x0039, 0xC1F9, 0x81F8, 0x4038, 0x0028, 0xC1E8, 0x81E9, 0x4029,
                                            0x01EB, 0xC02B, 0x802A, 0x41EA, 0x01EE, 0xC02E, 0x802F, 0x41EF, 0x002D, 0xC1ED,
                                            0x81EC, 0x402C, 0x01E4, 0xC024, 0x8025, 0x41E5, 0x0027, 0xC1E7, 0x81E6, 0x4026,
                                            0x0022, 0xC1E2, 0x81E3, 0x4023, 0x01E1, 0xC021, 0x8020, 0x41E0, 0x01A0, 0xC060, 
                                            0x8061, 0x41A1, 0x0063, 0xC1A3, 0x81A2, 0x4062, 0x0066, 0xC1A6, 0x81A7, 0x4067,
                                            0x01A5, 0xC065, 0x8064, 0x41A4, 0x006C, 0xC1AC, 0x81AD, 0x406D, 0x01AF, 0xC06F,
                                            0x806E, 0x41AE, 0x01AA, 0xC06A, 0x806B, 0x41AB, 0x0069, 0xC1A9, 0x81A8, 0x4068, 
                                            0x0078, 0xC1B8, 0x81B9, 0x4079, 0x01BB, 0xC07B, 0x807A, 0x41BA, 0x01BE, 0xC07E,
                                            0x807F, 0x41BF, 0x007D, 0xC1BD, 0x81BC, 0x407C, 0x01B4, 0xC074, 0x8075, 0x41B5, 
                                            0x0077, 0xC1B7, 0x81B6, 0x4076, 0x0072, 0xC1B2, 0x81B3, 0x4073, 0x01B1, 0xC071,
                                            0x8070, 0x41B0, 0x0050, 0xC190, 0x8191, 0x4051, 0x0193, 0xC053, 0x8052, 0x4192, 
                                            0x0196, 0xC056, 0x8057, 0x4197, 0x0055, 0xC195, 0x8194, 0x4054, 0x019C, 0xC05C,
	                                        0x805D, 0x419D, 0x005F, 0xC19F, 0x819E, 0x405E, 0x005A, 0xC19A, 0x819B, 0x405B, 
	                                        0x0199, 0xC059, 0x8058, 0x4198, 0x0188, 0xC048, 0x8049, 0x4189, 0x004B, 0xC18B,
	                                        0x818A, 0x404A, 0x004E, 0xC18E, 0x818F, 0x404F, 0x018D, 0xC04D, 0x804C, 0x418C,
	                                        0x0044, 0xC184, 0x8185, 0x4045, 0x0187, 0xC047, 0x8046, 0x4186, 0x0182, 0xC042,
	                                        0x8043, 0x4183, 0x0041, 0xC181, 0x8180, 0x4040};
            string line;
            string tmpName = this.openFileDialog1.FileName;

            if (tmpName != "")
            {
                int counter = 0;
                int cellCounter = 0;
                byte lineLength = 0;
                byte nextLineLength = 0;
                byte lineType = 0x00;
                byte nextLineType = 0x00;
                byte hexDataShift = 0;
                byte[] lineAddress = new byte[2];
                byte[] nextLineAddress = new byte[2];

                string[] allLines = File.ReadAllLines(tmpName);
                for (counter = 0; counter < allLines.Length - 1; counter++)
                {
                    byte[] tmp = HexToByte(allLines[counter]);
                    byte[] tmpNext = HexToByte(allLines[counter + 1]);
                    byte[] tmptmp = new byte[1];
                    lineLength = tmp[0];
                    nextLineLength = tmpNext[0];
                    lineAddress[0] = tmp[1];
                    lineAddress[1] = tmp[2];
                    nextLineAddress[0] = tmpNext[1];
                    nextLineAddress[1] = tmpNext[2];
                    lineType = tmp[3];
                    nextLineType = tmpNext[3];
                    //this.dataGridView1.Rows[counter].HeaderCell.Value = BitConverter.ToString(lineAddress);
                    byte cell = 0;
                    while ((cell < lineLength) & (lineType == 0))
                    {

                        tmptmp[0] = tmp[cell + 4];
                        // else tmptmp[0] = 0xff;
                        
                        if ((cellCounter != 7294) & (cellCounter != 7295))
                            crc = (ushort)(crctable[((crc >> 8) ^ tmptmp[0]) & 0xff] ^ (ushort)(crc << 8));
                        else
                        {
                            if (cellCounter == 7294) crcPrgLow = tmptmp[0]; //7134, 7135 - адреса ячеек, в которых записана контрольная сумма штатной прошивки
                            if (cellCounter == 7295) crcPrgHigh = tmptmp[0];
                        }
                        //this.dataGridView1.Rows[counter].Cells[cell].Value = ByteToHex(tmptmp);
                        cell++;
                        cellCounter++;
                        //if (cellCounter == 7134) cellCounter += 2;
                    }

                }
                
            }
            textBox2.Text = crc.ToString();
            if (crc == (((ushort)crcPrgHigh << 8) + crcPrgLow)) return true;
            else return false;
        }

        #region ByteToHex
        /// <summary>
        /// method to convert a byte array into a hex string
        /// </summary>
        /// <param name="comByte">byte array to convert</param>
        /// <returns>a hex string</returns>
        private string ByteToHex(byte[] comByte)
        {
            //create a new StringBuilder object
            StringBuilder builder = new StringBuilder(comByte.Length * 3);
            //loop through each byte in the array
            foreach (byte data in comByte)
                //convert the byte to a string and add to the stringbuilder
                builder.Append(Convert.ToString(data, 16).PadLeft(2, '0').PadRight(3, ' '));
            //return the converted value
            return builder.ToString().ToUpper();
        }
        #endregion
        #region HexToByte
        /// <summary>
        /// method to convert hex string into a byte array
        /// </summary>
        /// <param name="msg">string to convert</param>
        /// <returns>a byte array</returns>
        private byte[] HexToByte(string msg)
        {
            //remove any spaces from the string
            msg = msg.Replace(" ", "");
            msg = msg.Replace(":", "");
            msg = msg.Replace(".", "");
            msg = msg.Replace("-", "");
            msg = msg.Replace("\r\n", "");
            //create a byte array the length of the
            //string divided by 2
            byte[] comBuffer = new byte[msg.Length / 2];
            //loop through the length of the provided string
            for (int i = 0; i < msg.Length; i += 2)
                //convert each set of 2 characters to a byte
                //and add to the array
                comBuffer[i / 2] = (byte)Convert.ToByte(msg.Substring(i, 2), 16);
            //return the array
            //byte[] buf2 = HartProtocol.AppendCRC(comBuffer);
            return comBuffer;
        }
        #endregion 

        public void fillTableWithCode()
        {
            //this.SuspendLayout();
            string line;
            ListViewItem newItem;
            string tmpName = this.openFileDialog1.FileName;
           // this.ResumeLayout();
            
            if (tmpName != "")
            {
  
                string[] allLines = File.ReadAllLines(tmpName);

                int counter = 0;
                byte lineLength = 0;
                byte nextLineLength = 0;
                byte lineType = 0x00;
                byte nextLineType = 0x00;
                byte hexDataShift = 0;
                byte[] lineAddress = new byte[2];
                byte[] nextLineAddress = new byte[2];
                //StreamReader streamReader = new StreamReader(this.openFileDialog1.FileName);

                this.dataGridView1.Rows.Add(allLines.Length);
                //while ((line = streamReader.ReadLine()) != null)
                //this.toolStripProgressBar1.Minimum = 0;
                //this.toolStripProgressBar1.Maximum = allLines.Length;

                //this.toolStripProgressBar1.Value = 0;
                //this.toolStripStatusLabel1.Text = "Обработка...";
                //this.toolStripProgressBar1.Visible = true;
                for (counter = 0; counter < allLines.Length-1; counter++)
                {
                    byte[] tmp = HexToByte(allLines[counter]);
                    byte[] tmpNext = HexToByte(allLines[counter+1]);
                    byte[] tmptmp = new byte[1];
                    lineLength = tmp[0];
                    nextLineLength = tmpNext[0];
                    lineAddress[0] = tmp[1];
                    lineAddress[1] = tmp[2];
                    nextLineAddress[0] = tmpNext[1];
                    nextLineAddress[1] = tmpNext[2];
                    //while (lineAddress[1] % 16 != 0) lineAddress[1]++;
                    lineType = tmp[3];
                    nextLineType = tmpNext[3];
                    this.dataGridView1.Rows[counter].HeaderCell.Value = BitConverter.ToString(lineAddress);
                    //this.dataGridView1.Rows[counter].HeaderCell.Value = ByteToHex(lineAddress);
                    //foreach (DataGridViewColumn c in this.dataGridView1.Columns)
                    //{
                    //    c.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                    //}
                    byte cell = 0;
                    while ((cell < lineLength-hexDataShift) & (lineType == 0))
                    {

                        tmptmp[0] = tmp[cell+hexDataShift + 4];
                        // else tmptmp[0] = 0xff;
                        
                        this.dataGridView1.Rows[counter].Cells[cell].Value = ByteToHex(tmptmp);
                        cell++;
                        
                    }
                    if (counter == 0) hexDataShift=(byte)(16-cell);
                    //hexDataShift = cell;
                    //cell = 0;
                    while ((cell < 16)&(nextLineType==0))
                    {
                        tmptmp[0] = tmpNext[cell - (16-hexDataShift) + 4];
                        // else tmptmp[0] = 0xff;

                        this.dataGridView1.Rows[counter].Cells[cell].Value = ByteToHex(tmptmp);
                        cell++;
                    }
                    
                    //cell = 0;
                    if (counter == allLines.Length - 2)
                    {
                        while (cell < 16)
                        {
                            //if(cell<16)tmptmp[0] = tmpNext[cell + 4];
                            tmptmp[0] = 0xff;

                            this.dataGridView1.Rows[counter].Cells[cell].Value = ByteToHex(tmptmp);
                            cell++;
                        }
                    }
                   // cell = 0;
                    //this.toolStripProgressBar1.Value++;
                   // this.ResumeLayout();
                    //counter++;
                }
               // this.dataGridView1.Rows[counter-1].Visible = false;
                //this.ResumeLayout();
            }

        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
          //  dataGridView1.MultiSelect = true;
            if (dataGridView1.Rows.Count!=0)
                for (int i = dataGridView1.Rows.Count-1; i>=0; i--) dataGridView1.Rows.RemoveAt(i);
            
            //dataGridView1.SelectAll();
            //dataGridView1.ClearSelection();
          //  dataGridView1.MultiSelect = false;
            
            
            openFileDialog1.ShowDialog();
           // fillTableWithCode();
            if(checkCRC16())fillTableWithCode();
            else MessageBox.Show("Внимание! Открыт файл неверного формата, либо устаревший файл ПО ДВСТ-3. Выберите другой файл.", "Ошибка!", MessageBoxButtons.OK);
        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void numericUpDown2_ValueChanged(object sender, EventArgs e)
        {
            //HartProtocol.SlaveAddress = Convert.ToInt32(numericUpDown2.Value);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            //textBox1.Text = ByteToHex(getBytesFromGrid(1, 5));
            HartProtocol.SlaveAddress = Convert.ToInt32(numericUpDown1.Value);
            byte[] tmpAddr = new byte[1];
            tmpAddr[0] = Convert.ToByte(numericUpDown2.Value);
            byte[] tmpMes = HartProtocol.createMessageToSend(6, tmpAddr);
            numericUpDown1.Value = numericUpDown2.Value;
            HartProtocol.lastCommand = 6;
            templateProcessor((byte)dataExchangeTemplates.singleMessageTemplate, 6, tmpAddr, tmpAddr.Length);
            serialPort1.Write(tmpMes, 0, tmpMes.Length);
            textBox1.Text = ByteToHex(tmpMes);
        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            ListViewGroup newIG = new ListViewGroup();
            ListViewItem.ListViewSubItem subItem = new ListViewItem.ListViewSubItem();
           
            listView1.ShowGroups = true;
            for (int i = 1; i < 15; i++)
            {

                ListViewItem newItem;
                if (devicesArr[i].enabled)
                {
                    devicesArr[i].setValues(i % 3);

                    //newItem.SubItems.Add(devicesArr[i].address.ToString());
                    //newItem.SubItems.Add(devicesArr[i].devName);
                    //newItem.SubItems.Add(devices.firmware.bootloader.ToString());
                    //newItem.SubItems.Add(devicesArr[i].serialNum.ToString());
                    //newItem.SubItems.Add(devicesArr[i].firmwareRevision.ToString());
                    newItem = new ListViewItem(devicesArr[i].devs);
                    listView1.Items.Add(newItem);
                }
                
                
            }
        }

        public void setCellValue(int address, byte[] value)
        {
            int beginAddress;
            int endAddress;
            byte[] tmp = new byte[2];
            tmp = HexToByte(dataGridView1.Rows[0].HeaderCell.Value.ToString());
            beginAddress =(int)tmp[1] << 8 + tmp[0];
            endAddress = beginAddress + 16 * dataGridView1.RowCount;
            if ((address <= endAddress) & (address >= beginAddress)&(address+value.Length <=endAddress))
            {
                int tmpRow = (address-beginAddress)/16;//вот координаты интересующей нас ячейки
                int tmpCol = (address-beginAddress)%16;//дальше можно делать с ней что угодно
                int colCounter = tmpCol;
                int rowCounter = tmpRow;
                byte[] tmpVal = new byte[1];
                for (int i = 0; i < value.Length; i++)
                {
                    if (colCounter >= 16)
                    {
                        colCounter = 0;
                        rowCounter++;
                    }
                    tmpVal[0] = value[i];
                    dataGridView1.Rows[rowCounter].Cells[colCounter].Value = ByteToHex(tmpVal);
                    colCounter++;
                }
            }

        }
        /*
         * Эта функция возвращает кусок datagridview заданной длины
         * если заданная длина равна 0, то возвращаем массив из 2х элементов 0xED 0xFE //end of file
         */ 
        public byte[] getBytesFromGrid(int address, int length)
        {
            byte[] retArr;
            if(length==0)
            {
                retArr   = new byte[2];
                retArr[0] = 0xed;
                retArr[1] = 0xfe;
                return retArr;
            }
            retArr = new byte[length];
            int beginAddress;
            int endAddress;
            byte[] tmp = new byte[2];
            tmp = HexToByte(dataGridView1.Rows[0].HeaderCell.Value.ToString());
            beginAddress = (int)tmp[1] << 8 + tmp[0];
            endAddress = beginAddress + 16 * (dataGridView1.RowCount-1);
            if ((address < endAddress) & (address >= beginAddress) & (address + length <= endAddress))
            {
                int tmpRow = (address - beginAddress) / 16;//вот координаты интересующей нас ячейки
                int tmpCol = (address - beginAddress) % 16;//дальше можно делать с ней что угодно
                int colCounter = tmpCol;
                int rowCounter = tmpRow;
                byte[] tmpVal = new byte[1];
                if (dataGridView1.Rows[rowCounter].Cells[colCounter].Value != null)
                {
                    for (int i = 0; i < length; i++)
                    {
                        if (colCounter >= 16)
                        {
                            colCounter = 0;
                            rowCounter++;
                        }
                        if (dataGridView1.Rows[rowCounter].Cells[colCounter].Value != null)
                            tmpVal = HexToByte(dataGridView1.Rows[rowCounter].Cells[colCounter].Value.ToString());
                        else tmpVal[0] = 0xff;
                        retArr[i] = tmpVal[0];
                        //dataGridView1.Rows[rowCounter].Cells[colCounter].Value = ByteToHex(tmpVal);
                        colCounter++;
                    }
                }
                else
                {
                    for (int i = 0; i < length; i++)
                    {
                        retArr[i] = 0xff;
                    }
                }
            }
            else
            {
                for (int i = 0; i < length; i++)
                {
                    retArr[i] = 0xff;
                }
            }

            return retArr;
        }
        /* эта функция собирает шаблон для работы процессора обмена сообщений
         * она создает массив номеров команд и обнуляет индекс массива
         * входные данные -  сценарий обмена, команда, данные, длина сообщения
         */
        public void templateProcessor(byte dataReq, byte command, byte[] data, int length) 
        {
            templateSelected = dataReq;
            progressBarCounter = 0;
            switch (dataReq)
            {
                case 0:
                    {
                        
                        templateArr = new byte[length];
                        templateArr[0] = command;
                        dataToWriteInPort = HartProtocol.createMessageToSend(command, data);
                        HartProtocol.lastCommand = command;
                        templateArrIndex = 0;
                        //serialPort1.Write(dataToWriteInPort, 0, dataToWriteInPort.Length);
                        timer4.Interval = 650;
                        timer4.Start();
                         return;
                    }
                case 1:
                    {

                        templateArr = new byte[length*2];
                        //templateArr[0] = 0; //спрашиваем идшник девайса
                        HartProtocol.SlaveAddress = firstSearchAddress;
                        dataToWriteInPort = HartProtocol.createMessageToSend(command, data);
                        //templateArr[1] = 1; // и его серийник
                        for (int i = 0; i < templateArr.Length; i++) //templateArr[i] = 0x00;
                        {
                            if ((i % 2) != 0) templateArr[i] = 16;
                            else templateArr[i] = 0x00;
                        }
                        serialPort1.Write(dataToWriteInPort, 0, dataToWriteInPort.Length);
                        textBox3.AppendText("отправлен запрос на чтение идентификатора устройства по адресу " + Convert.ToString(HartProtocol.SlaveAddress) + "\r\n");
                        textBox3.AppendText(ByteToHex(dataToWriteInPort));
                        textBox3.AppendText("\r\n");
                        templateArrIndex = 0;
                        timer3.Interval = 650;
                        timer3.Start();
                        timer2.Start();
                        toolStripStatusLabel1.Text = "Выполняется поиск устройств...";
                        toolStripProgressBar1.Minimum = 0;
                        toolStripProgressBar1.Maximum = 15;
                        toolStripProgressBar1.Visible = true;
                        return;
                    }
                case 2:
                    {
                        /*
                         * важный момент состоит в том, что команда записи страничного сегмента в память мк - 0xAA
                         * должна приходить, пока не будет заполнена вся страница, т.е. если не хватает еще 2х сегментов страниц 
                         * данных, заполняем их oxff;
                         * таким образом удастся заметно сократить код бутлодера, и он будет перезагружаться сразу по получению сообщения
                         * о конце записи 0xAB
                         */

                        while(length%128!=0)length+=16;                  //добавляем ff до размера номинальной страницы флеш
                        templateArr = new byte[length/ 32 + 1];
                        for (int i = 0; i < templateArr.Length-1; i++)//шлем очень много 32х байтных сообщений
                            templateArr[i] = 0xAA;
                        templateArr[templateArr.Length-1] = 0xAB; //и одно сообщение о том, что данных больше нет
                        dataToWriteInPort = HartProtocol.createMessageToSend(0xAA, data);
                        HartProtocol.lastCommand = 0xAA;
                        templateArrIndex = 0;
                        timer1.Interval = 650;
                        serialPort1.Write(dataToWriteInPort, 0, dataToWriteInPort.Length);
                        textBox3.AppendText("отправлено сообщение № " + (templateArrIndex+1).ToString() + "\r\n");
                        textBox3.AppendText(ByteToHex(dataToWriteInPort));
                        textBox3.AppendText("\r\n");
                        timer2.Start();
                        timer1.Start();
                        toolStripStatusLabel1.Text = "Выполняется прошивка устройства...";
                        toolStripProgressBar1.Visible = true;
                        toolStripProgressBar1.Minimum = 0;
                        toolStripProgressBar1.Maximum = length/32;
                        toolStripProgressBar1.Value = 0;
                        progressBarCounter = 0;
                        return;
                    }
            }

            
        }

        //public byte answerProcessor(
        private void button1_Click(object sender, EventArgs e)
        {
           // byte[] tmp = new byte[4];
            
            //if (!serialPort1.IsOpen)
            //{
                //serialPort1.PortName = toolStripComboBox1.SelectedItem.ToString();
                //serialPort1.Open();
               // templateArrIndex=0;
                tableCellDataIndex = 0;
                templateProcessor((byte)dataExchangeTemplates.flashLoadTemplate, 1, getBytesFromGrid(tableCellDataIndex, 32), (dataGridView1.Rows.Count-1) * 16);
         //   }
          //requestProcessor(tmp, (byte)dataExchangeTemplates.deviceSearchTemplate);
          //setCellValue(Convert.ToInt32(numericUpDown1.Value), HexToByte(textBox2.Text.ToString()));
          //  HartProtocol.SlaveAddress = (byte)numericUpDown2.Value;
          //  HartProtocol.NumberOfPreambulas = 3;
          //  textBox2.Text = B yteToHex(HartProtocol.createMessageToSend((byte)numericUpDown1.Value, HexToByte(textBox1.Text.ToString())));
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            toolStripComboBox1.Items.AddRange(System.IO.Ports.SerialPort.GetPortNames());
            toolStripComboBox1.SelectedIndex = 0;
            //this.ParentForm.Width = 749;
            //this.Size = new System.Drawing.Size(749, 616);
            this.Size = new Size(749, 616);
            //this.Size(749,616);
        }
        /*Этот таймер используется для отсчета времени до следующей отправки сообщения
         *вообще же два таймера нужны для более простого и понятного интерфейса управления
         */
        private void timer1_Tick(object sender, EventArgs e)
        {
            timer1.Stop();

            if (templateArrIndex < templateArr.Length - 1)
            {
                progressBarCounter++;
                templateArrIndex++;
                tableCellDataIndex += 32;
                HartProtocol.lastCommand = templateArr[templateArrIndex];
                byte[] tmpArr1 = getBytesFromGrid(tableCellDataIndex, 16);
                byte[] tmpArr2 = getBytesFromGrid(tableCellDataIndex + 16, 16);
                Array.Resize(ref tmpArr1, 32);
                for (int i = 16; i < 32; i++) tmpArr1[i] = tmpArr2[i - 16];
                dataToWriteInPort = HartProtocol.createMessageToSend(templateArr[templateArrIndex], tmpArr1);
                serialPort1.Write(dataToWriteInPort, 0, dataToWriteInPort.Length);
                textBox3.AppendText("отправлено сообщение № " + (templateArrIndex + 1).ToString() + "\r\n");
                textBox3.AppendText(ByteToHex(dataToWriteInPort));
                textBox3.AppendText("\r\n");
                if (templateArrIndex % 2 == 0) toolStripStatusLabel1.Text = "Выполняется прошивка устройства.. ";
                else toolStripStatusLabel1.Text = "Выполняется прошивка устройства...";
                timer2.Start();
                timer1.Start();
            }
            else
            {
                toolStripStatusLabel1.Text = "Устройство прошито";
                toolStripProgressBar1.Visible = false;
            }
            
        }

        private void timer2_Tick(object sender, EventArgs e)
        {

            timer2.Stop();
            //if()progressBarCounter++;
            if (serialPort1.BytesToRead - 3> HartProtocol.GetCommandDataLength(HartProtocol.lastCommand))
            {
                answerTimedOut = false;
                incomingMessageProcessor();// HartProtocol.GetCommandDataLength(HartProtocol.lastCommand)) incomingMessageProcessor();
                if ((ReadDataCRCOk)&&(templateSelected==1))
                {

                        devicesArr[HartProtocol.SlaveAddress].address = Convert.ToByte(HartProtocol.SlaveAddress);
                        devicesArr[HartProtocol.SlaveAddress].devType = Convert.ToByte((Convert.ToByte(HartProtocol.DevTypeCode) & 0x10)>>4);
                        devicesArr[HartProtocol.SlaveAddress].devName = "ДВСТ-" + Convert.ToString(Convert.ToByte(HartProtocol.DevTypeCode) & 0x0f);
                        devicesArr[HartProtocol.SlaveAddress].serialNum = HartProtocol.DevSerialNumber;
                        devicesArr[HartProtocol.SlaveAddress].firmwareRevision = Convert.ToByte(HartProtocol.SoftwareRev);
                        devicesArr[HartProtocol.SlaveAddress].enabled = true;
                        ListViewItem newItem;
                        if ((devicesArr[HartProtocol.SlaveAddress].enabled)&&(templateArrIndex%2!=0))
                        {
                            devicesArr[HartProtocol.SlaveAddress].setValues(devicesArr[HartProtocol.SlaveAddress].devType);

                            //newItem.SubItems.Add(devicesArr[i].address.ToString());
                            //newItem.SubItems.Add(devicesArr[i].devName);
                            //newItem.SubItems.Add(devices.firmware.bootloader.ToString());
                            //newItem.SubItems.Add(devicesArr[i].serialNum.ToString());
                            //newItem.SubItems.Add(devicesArr[i].firmwareRevision.ToString());
                            newItem = new ListViewItem(devicesArr[HartProtocol.SlaveAddress].devs);
                            listView1.Items.Add(newItem);
                        }
                }
                if ((ReadDataCRCOk) && (templateSelected == 2))
                {
                    //CRCErrorsCounter++;
                    //this.label3.Text = "Ошибок приёма:" + CRCErrorsCounter.ToString();
                    if (progressBarCounter > toolStripProgressBar1.Value) toolStripProgressBar1.Value++;
                }
            }
            else
            {
                //ReadBytesLastCycle = serialPort1.BytesToRead;
                answerTimedOut = true;
                
                textBox3.AppendText(DateTime.Now.ToString() + "  таймаут ответа от устройства по адресу " + Convert.ToString(HartProtocol.SlaveAddress) + "\r\n");
                //timer2.Start();
            }
            if ((templateArrIndex % 2 != 0)&&(templateArr[templateArrIndex]<=0x10))
           // if (templateArr[templateArrIndex] == 0x00)
                if (lastSearchAddress != HartProtocol.SlaveAddress)
                {
                    HartProtocol.SlaveAddress++;
                    //progressBarCounter++;
                    toolStripProgressBar1.Value++;
                }
        }

        private void button3_Click(object sender, EventArgs e)
        {
           // if (!serialPort1.IsOpen)
            
                
            //    serialPort1.Open();
                //HartProtocol.SlaveAddress = Convert.ToInt32(numericUpDown1.Value);
            byte[] tmpData = new byte[32];
            for (byte i = 0; i < 32; i++)
            {
                tmpData[i] = i;
            }
            byte[] tmpByteArr = HartProtocol.createMessageToSend(0, tmpData);
            textBox1.Text = ByteToHex(tmpByteArr);
            HartProtocol.lastCommand = tmpByteArr[6];
            serialPort1.Write(tmpByteArr, 0, tmpByteArr.Length);
            timer2.Start();
 
                //numericUpDown1.Value = numericUpDown2.Value;
                //dataToWriteInPort = HartProtocol.createMessageToSend(templateArr[templateArrIndex], getBytesFromGrid(tableCellDataIndex, 32));
                //templateProcessor((byte)dataExchangeTemplates.flashLoadTemplate, 1, getBytesFromGrid(tableCellDataIndex, 32), (dataGridView1.Rows.Count-1) * 16);
            
        }

        private void incomingMessageProcessor()
        {

            int i = 0;

            {
                //int dataQ = HartProtocol.GetCommandDataLength(HartProtocol.lastCommand);
                //if ((serialPort1.BytesToRead > dataQ))
                //{

                byte[] buffer = new byte[serialPort1.BytesToRead];//new byte[serialPort1.BytesToRead];

                //for (i = 0; i < dataQ; i++)// (serialPort1.BytesToRead > 0) 
                //{
                //    buffer[i] = (byte)serialPort1.ReadByte();

                //}
                this.serialPort1.Read(buffer, 0, serialPort1.BytesToRead);
                //Array.Reverse(buffer);


                if (HartProtocol.CheckMessageIntegrity(buffer))
                {
                    // HartProtocol.CutOffGhostBytes(buffer);
                    Array.Reverse(buffer);
                    spRead = ByteToHex(buffer);

                    byte[] buffer_ = HartProtocol.CutOffPreambulasRecieved(buffer);
                    buffer_ = HartProtocol.CutOffGhostBytes(buffer_);
                    if (HartProtocol.CheckCRC(buffer_) == 1)
                    {
                        spRead += " ---> CRC OK!";
                        MessagesRecieved++;
                        ReadDataCRCError = 0;
                        ReadDataCRCOk = true;
                        
                    }
                    else
                    {
                        spRead += " ---> CRC Wrong!";
                        MessagesRecieved++;
                        CRCerrors++;
                        ReadDataCRCError++;
                        ReadDataCRCOk = false;
                    }

                    HartProtocol.GenerateAnswer(buffer_);


                    this.demoThread =
                        new Thread(new ThreadStart(this.ThreadProcSafe));

                    this.demoThread.Start();
                    // ReadBytesLastCycle = 0;
                    this.serialPort1.DiscardInBuffer();
                    this.serialPort1.DiscardOutBuffer();
                    this.serialPort1.Close();
                    this.serialPort1.Open();
                    ReadBytesLastCycle = 0;
                    
                }
                // }
            }
            // ReadBytesLastCycle = serialPort1.BytesToRead;
        }
        private void ThreadProcSafe()
        {
            this.SetText(spRead);
        }
        private void SetText(string text)
        {
            // InvokeRequired required compares the thread ID of the
            // calling thread to the thread ID of the creating thread.
            // If these threads are different, it returns true.

            if (this.textBox3.InvokeRequired)
            {
                SetTextCallback d = new SetTextCallback(SetText);
                this.Invoke(d, new object[] { text });
            }
            else
            {
                // this.textBox2.Clear();
                //text += "\r\n";
           //     this.label18.Text = "принято корректных сообщений " + Math.Round(((Convert.ToDouble(MessagesRecieved - CRCerrors)) * 100 / MessagesRecieved), 1).ToString() + " %";
                this.textBox3.AppendText(DateTime.Now.ToString() + " ---> ");
                this.textBox3.AppendText(text);//  = text;
                this.textBox3.AppendText("\r\n");
            }
        }

        private void toolStripComboBox1_DropDownClosed(object sender, EventArgs e)
        {

            if (!serialPort1.IsOpen)
            {
                serialPort1.PortName = toolStripComboBox1.SelectedItem.ToString();
                serialPort1.Open();
            }
            else
            {
                serialPort1.Close();
                serialPort1.PortName = toolStripComboBox1.SelectedItem.ToString();
                serialPort1.Open();
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (this.button4.Text == "Начать поиск")
            {
                this.toolStripProgressBar1.Value = 0;
                listView1.Items.Clear();
                button4.Text = "Остановить поиск";
                byte[] tmpMsg = new byte[1];
                tmpMsg[0] = 0x00;
                templateProcessor((byte)dataExchangeTemplates.deviceSearchTemplate, 0, tmpMsg, (lastSearchAddress - firstSearchAddress + 1));
            }
            else
            {
                this.toolStripProgressBar1.Value = 15;
                this.toolStripStatusLabel1.Text = "Поиск устройств прерван пользователем";
                toolStripProgressBar1.Visible = false;
                button4.Text = "Начать поиск";
                timer2.Stop();
                timer3.Stop();
            }
            
        }

        public void askDeviceID()
        {

        }
        //этот таймер будет использоваться для работы с поиском устройств,
        //он будет отсчитывать время до отправки следующего сообщения
        private void timer3_Tick(object sender, EventArgs e)
        {

            if (templateArrIndex < templateArr.Length - 1)
            {
                if ((ReadDataCRCOk) | (answerTimedOut) | (ReadDataCRCError >= 3)) templateArrIndex++;

                byte[] tmpMsg = new byte[1];
                tmpMsg[0] = 0x00;
                HartProtocol.lastCommand = templateArr[templateArrIndex];
                dataToWriteInPort = HartProtocol.createMessageToSend(templateArr[templateArrIndex], tmpMsg);
                serialPort1.Write(dataToWriteInPort, 0, dataToWriteInPort.Length);
                if (templateArrIndex % 2 == 0)
                {
                    textBox3.AppendText(DateTime.Now.ToString() + "  отправлен запрос на чтение идентификатора устройства по адресу " + Convert.ToString(HartProtocol.SlaveAddress) + "\r\n");
                }
                else
                {
                    textBox3.AppendText(DateTime.Now.ToString() + "  отправлен запрос на чтение серийного номера устройства по адресу " + Convert.ToString(HartProtocol.SlaveAddress) + "\r\n");

                }
                textBox3.AppendText(ByteToHex(dataToWriteInPort));
                textBox3.AppendText("\r\n");
                timer2.Start();
                timer3.Start();
            }
            else
            {
                toolStripStatusLabel1.Text = "Поиск устройств завершен";
                templateArrIndex = 0;
                button4.Text = "Начать поиск";
                toolStripProgressBar1.Visible = false;
            }
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            numericUpDown2.Value = Convert.ToDecimal(listView1.FocusedItem.Text);
            HartProtocol.SlaveAddress = Convert.ToByte(listView1.FocusedItem.Text);
        }

        private void button6_Click(object sender, EventArgs e)
        {
            if (button6.Text == ">>")
            {
                this.Size = new Size(1043, 616);
                this.groupBox5.Visible = true;
                this.button6.Text = "<<";
            }
            else
            {
                this.groupBox5.Visible = false;
                this.Size = new Size(749, 616);
                
                this.button6.Text = ">>";
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            HartProtocol.SlaveAddress = Convert.ToInt32(numericUpDown1.Value);
            byte[] tmpSerial = new byte[3];
            tmpSerial[0] = Convert.ToByte(Convert.ToInt32(numericUpDown3.Value) >> 16);
            tmpSerial[1] = Convert.ToByte((Convert.ToInt32(numericUpDown3.Value) >> 8)& 0x00ff);
            tmpSerial[2] = Convert.ToByte(Convert.ToInt32(numericUpDown3.Value) & 0x0000ff);
            byte[] tmpMes=  HartProtocol.createMessageToSend(19, tmpSerial);
            HartProtocol.lastCommand = 19;
            templateProcessor((byte)dataExchangeTemplates.singleMessageTemplate, 19, tmpSerial, tmpSerial.Length);
            serialPort1.Write(tmpMes, 0, tmpMes.Length);
            textBox1.Text = ByteToHex(tmpMes);
            
            //numericUpDown1.Value = numericUpDown2.Value;
        }

        private void timer4_Tick(object sender, EventArgs e)
        {
            timer4.Stop();

            if (serialPort1.BytesToRead - 3 > HartProtocol.GetCommandDataLength(HartProtocol.lastCommand))
            {
                answerTimedOut = false;
                incomingMessageProcessor();// HartProtocol.GetCommandDataLength(HartProtocol.lastCommand)) incomingMessageProcessor();
                if ((ReadDataCRCOk)&&(HartProtocol.lastCommand==19))
                {
                    textBox3.AppendText("Установлен серийный номер" + numericUpDown3.Value.ToString() + "\r\n");
                    devicesArr[HartProtocol.SlaveAddress].serialNum = Convert.ToInt32(numericUpDown3.Value);
                    ListViewItem newItem;
                    listView1.Items.RemoveAt(0);//вообще нужно поменять для выбранного индекса.
                    devicesArr[HartProtocol.SlaveAddress].setValues(devicesArr[HartProtocol.SlaveAddress].devType);
                    newItem = new ListViewItem(devicesArr[HartProtocol.SlaveAddress].devs);
                    listView1.Items.Add(newItem);

                }
                if ((ReadDataCRCOk) && (HartProtocol.lastCommand == 6))
                {
                    textBox3.AppendText("Изменен адрес устройства на " + numericUpDown2.Value.ToString() + "\r\n");
                    devicesArr[HartProtocol.SlaveAddress].address = Convert.ToByte(numericUpDown2.Value);
                    ListViewItem newItem;
                    listView1.Items.RemoveAt(0);//вообще нужно поменять для выбранного индекса.
                    devicesArr[HartProtocol.SlaveAddress].setValues(devicesArr[HartProtocol.SlaveAddress].devType);
                    newItem = new ListViewItem(devicesArr[HartProtocol.SlaveAddress].devs);
                    listView1.Items.Add(newItem);

                }
            }
            else
            {
                //ReadBytesLastCycle = serialPort1.BytesToRead;
                answerTimedOut = true;

                textBox3.AppendText(DateTime.Now.ToString() + "  таймаут ответа от устройства по адресу " + Convert.ToString(HartProtocol.SlaveAddress) + "\r\n");
                //timer2.Start();
            }

        }

        private void button7_Click(object sender, EventArgs e)
        {
            byte[] eraseMes = new byte[5]{0x45,0x52,0x41,0x53,0x45};
            byte[] tmpMes = HartProtocol.createMessageToSend(42, eraseMes);
            templateProcessor((byte)dataExchangeTemplates.singleMessageTemplate, 42, eraseMes, eraseMes.Length);
            serialPort1.Write(tmpMes, 0, tmpMes.Length);
            textBox1.Text = ByteToHex(tmpMes);
            toolStripStatusLabel1.Text = "Стирание программы...";
            toolStripProgressBar1.Visible = true;
            progressBarCounter = 0;
            toolStripProgressBar1.Maximum = 500;
            toolStripProgressBar1.Value = 0;
            panel1.Enabled = false;
            toolStrip1.Enabled = false;
            timer5.Start();
            
        }

        private void timer5_Tick(object sender, EventArgs e)
        {
            toolStripProgressBar1.Value++;
            if (toolStripProgressBar1.Value < 500) timer5.Start();
            else
            {
                panel1.Enabled = true;
                toolStripProgressBar1.Value = 0;
                toolStripProgressBar1.Visible = false;
                toolStrip1.Enabled = true;
                //toolStripStatusLabel1.Visible = false;
                toolStripStatusLabel1.Text = "Стирание программы завершено";
                timer5.Stop();
            }
        }
        

        
    }
}
